using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using OrchestrationApi.Utils;
using SqlSugar;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 健康检查报表控制器
/// </summary>
[ApiController]
[Route("admin/health-check")]
[Authorize]
public class HealthCheckReportController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly IKeyManager _keyManager;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<HealthCheckReportController> _logger;

    public HealthCheckReportController(
        IHealthCheckService healthCheckService,
        IKeyManager keyManager,
        ISqlSugarClient db,
        ILogger<HealthCheckReportController> logger)
    {
        _healthCheckService = healthCheckService;
        _keyManager = keyManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 获取健康检查报表数据
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetHealthCheckReport()
    {
        try
        {
            var groups = await _keyManager.GetAllGroupsAsync();
            var allStats = await _healthCheckService.GetAllHealthCheckStatsAsync();
            
            var reportData = new List<object>();

            foreach (var group in groups.Where(g => g.Enabled && g.HealthCheckEnabled))
            {
                var groupStats = allStats.Where(s => s.GroupId == group.Id).ToList();
                
                // 服务商健康状态
                var providerStats = groupStats.FirstOrDefault(s => s.CheckType == HealthCheckTypes.Provider);
                
                // Key健康状态统计
                var keyStats = groupStats.Where(s => s.CheckType == HealthCheckTypes.ApiKey).ToList();
                
                // 模型健康状态统计
                var modelStats = groupStats.Where(s => s.CheckType == HealthCheckTypes.Model).ToList();

                var groupReport = new
                {
                    group_id = group.Id,
                    group_name = group.GroupName,
                    provider_type = group.ProviderType,
                    base_url = group.BaseUrl,
                    enabled = group.Enabled,
                    
                    // 服务商健康状态
                    provider_health = new
                    {
                        status = GetHealthStatus(providerStats),
                        last_check = providerStats?.LastCheckAt,
                        last_success = providerStats?.LastSuccessAt,
                        last_failure = providerStats?.LastFailureAt,
                        success_rate = CalculateSuccessRate(providerStats),
                        avg_response_time = providerStats?.AvgResponseTimeMs ?? 0,
                        consecutive_failures = providerStats?.ConsecutiveFailures ?? 0
                    },
                    
                    // Key健康状态汇总
                    keys_health = new
                    {
                        total_keys = keyStats.Count,
                        healthy_keys = keyStats.Count(s => GetHealthStatus(s) == "healthy"),
                        unhealthy_keys = keyStats.Count(s => GetHealthStatus(s) == "unhealthy"),
                        last_check = keyStats.Count > 0 ? keyStats.Max(s => s.LastCheckAt) : (DateTime?)null,
                        avg_success_rate = keyStats.Count > 0 ? keyStats.Average(s => CalculateSuccessRate(s)) : 0
                    },

                    // 模型健康状态汇总
                    models_health = await CalculateGroupModelHealthStats(group, modelStats)
                };

                reportData.Add(groupReport);
            }

            return Ok(new { success = true, data = reportData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取健康检查报表数据时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组详细健康检查数据
    /// </summary>
    [HttpGet("group/{groupId}/details")]
    public async Task<IActionResult> GetGroupHealthDetails(string groupId)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId && !g.IsDeleted)
                .FirstAsync();
            if (group == null)
            {
                return NotFound(new { success = false, error = "分组不存在" });
            }

            var stats = await _healthCheckService.GetHealthCheckStatsAsync(groupId);
            var recentResults = await _healthCheckService.GetRecentHealthCheckResultsAsync(groupId, null, 50);

            // 按检查类型分组最近的结果
            var providerResults = recentResults.Where(r => r.CheckType == HealthCheckTypes.Provider).ToList();
            var keyResults = recentResults.Where(r => r.CheckType == HealthCheckTypes.ApiKey).ToList();
            var modelResults = recentResults.Where(r => r.CheckType == HealthCheckTypes.Model).ToList();

            // 获取服务商健康状态统计
            var providerStats = stats.FirstOrDefault(s => s.CheckType == HealthCheckTypes.Provider);
            var keyStatsGroup = stats.Where(s => s.CheckType == HealthCheckTypes.ApiKey).ToList();
            var modelStatsGroup = stats.Where(s => s.CheckType == HealthCheckTypes.Model).ToList();

            // 构建前端期望的扁平化数据结构
            var detailsData = new
            {
                // 基本信息
                group_name = group.GroupName,
                provider_type = group.ProviderType,
                base_url = group.BaseUrl,
                enabled = group.Enabled,

                // 服务商健康状态
                provider_health = new
                {
                    status = GetHealthStatus(providerStats),
                    last_check = providerStats?.LastCheckAt,
                    avg_response_time = providerStats?.AvgResponseTimeMs ?? 0,
                    success_rate = CalculateSuccessRate(providerStats),
                    error_message = providerResults.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.ErrorMessage
                },

                // 密钥健康状态统计
                keys_health = new
                {
                    total_keys = keyStatsGroup.Count,
                    healthy_keys = keyStatsGroup.Count(s => GetHealthStatus(s) == "healthy")
                },

                // 密钥详细信息
                keys = keyResults.GroupBy(r => r.ApiKeyHash).Select(g => new
                {
                    key_id = g.Key,
                    masked_key = g.FirstOrDefault()?.ApiKeyMasked ?? "sk-****************************",
                    health_status = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.IsSuccess == true ? "healthy" : "unhealthy",
                    success_rate = CalculateSuccessRateFromResults(g.ToList()),
                    avg_response_time = g.Where(r => r.ResponseTimeMs > 0).Average(r => (double)r.ResponseTimeMs),
                    last_check = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.CheckedAt,
                    error_message = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.ErrorMessage
                }).ToList(),

                // 模型健康状态统计 - 使用与主列表相同的计算方法
                models_health = await CalculateGroupModelHealthStats(group, modelStatsGroup),

                // 模型详细信息
                models = modelResults.GroupBy(r => r.ModelId).Select(g => new
                {
                    model_name = g.Key,
                    health_status = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.IsSuccess == true ? "healthy" : "unhealthy",
                    success_rate = CalculateSuccessRateFromResults(g.ToList()),
                    avg_response_time = g.Where(r => r.ResponseTimeMs > 0).Average(r => (double)r.ResponseTimeMs),
                    last_check = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.CheckedAt,
                    error_message = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.ErrorMessage
                }).ToList()
            };

            return Ok(new { success = true, data = detailsData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组健康检查详细数据时发生异常: {GroupId}", groupId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组的健康检查一致性分析
    /// </summary>
    [HttpGet("groups/{groupId}/analysis")]
    public async Task<IActionResult> GetGroupHealthAnalysis(string groupId)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId && !g.IsDeleted)
                .FirstAsync();

            if (group == null)
            {
                return NotFound(new { error = "分组不存在" });
            }

            // 获取最近的健康检查结果
            var recentResults = await _healthCheckService.GetRecentHealthCheckResultsAsync(groupId, null, 100);

            if (!recentResults.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = "暂无健康检查数据",
                    group_id = groupId,
                    group_name = group.GroupName
                });
            }

            // 分析健康检查一致性
            var analysis = _healthCheckService.AnalyzeHealthCheckConsistency(recentResults);

            // 按类型分组最新结果
            var latestByType = recentResults
                .GroupBy(r => r.CheckType)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CheckedAt).First());

            var result = new
            {
                success = true,
                group_id = groupId,
                group_name = group.GroupName,
                provider_type = group.ProviderType,
                base_url = group.BaseUrl,
                analysis_time = DateTime.Now,

                // 一致性分析结果
                consistency_analysis = new
                {
                    provider_healthy = analysis.ProviderHealthy,
                    keys_healthy = analysis.KeysHealthy,
                    models_healthy = analysis.ModelsHealthy,
                    is_inconsistent = analysis.IsInconsistent,
                    inconsistency_reason = analysis.InconsistencyReason,
                    overall_healthy = analysis.IsOverallHealthy,
                    success_rate = Math.Round(analysis.SuccessRate, 2),
                    status_summary = analysis.GetStatusSummary(),
                    detailed_explanation = analysis.GetDetailedExplanation()
                },

                // 最新检查结果
                latest_results = latestByType.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        is_healthy = kvp.Value.IsHealthy(),
                        status_code = kvp.Value.StatusCode,
                        response_time_ms = kvp.Value.ResponseTimeMs,
                        error_message = kvp.Value.ErrorMessage,
                        checked_at = kvp.Value.CheckedAt
                    }
                ),

                // 统计信息
                statistics = new
                {
                    total_checks = analysis.TotalChecks,
                    successful_checks = analysis.SuccessfulChecks,
                    failed_checks = analysis.FailedChecks,
                    data_period = new
                    {
                        from = recentResults.Min(r => r.CheckedAt),
                        to = recentResults.Max(r => r.CheckedAt)
                    }
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组健康检查分析时发生异常: {GroupId}", groupId);
            return StatusCode(500, new { success = false, error = "获取分析失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 手动触发健康检查
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerHealthCheck([FromBody] TriggerHealthCheckRequest request)
    {
        try
        {
            List<HealthCheckResult> results;

            if (!string.IsNullOrEmpty(request.GroupId))
            {
                // 检查指定分组
                results = await _healthCheckService.CheckGroupCompleteHealthAsync(request.GroupId);
            }
            else
            {
                // 检查所有分组
                results = await _healthCheckService.CheckAllGroupsHealthAsync();
            }

            if (results.Count > 0)
            {
                await _healthCheckService.SaveHealthCheckResultsAsync(results);
            }

            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count - successCount;

            return Ok(new
            {
                success = true,
                message = "健康检查已完成",
                data = new
                {
                    total_checks = results.Count,
                    successful_checks = successCount,
                    failed_checks = failureCount,
                    results = results.Select(r => new
                    {
                        group_id = r.GroupId,
                        check_type = r.CheckType,
                        status_code = r.StatusCode,
                        response_time_ms = r.ResponseTimeMs,
                        is_success = r.IsSuccess,
                        error_message = r.ErrorMessage,
                        checked_at = r.CheckedAt
                    })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动触发健康检查时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取健康检查统计概览
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetHealthCheckOverview()
    {
        try
        {
            var allStats = await _healthCheckService.GetAllHealthCheckStatsAsync();
            var groups = await _keyManager.GetAllGroupsAsync();
            var enabledGroups = groups.Where(g => g.Enabled && g.HealthCheckEnabled).ToList();

            // 获取启用分组的GroupId列表
            var enabledGroupIds = enabledGroups.Select(g => g.Id).ToHashSet();

            // 只统计启用分组的健康检查数据
            var enabledStats = allStats.Where(s => enabledGroupIds.Contains(s.GroupId)).ToList();

            // 计算配置的总数（与GetHealthCheckReport方法保持一致）
            var totalConfiguredKeys = enabledGroups.Sum(g => GetApiKeyCount(g));
            var totalConfiguredModels = enabledGroups.Sum(g => GetModelCount(g));

            // 基于HealthCheckResult按ModelId统计模型健康状态
            var enabledGroupIdsList = enabledGroupIds.ToList();
            var (healthyModels, unhealthyModels) = await CalculateModelHealthStatsFromResults(enabledGroupIdsList);
            var modelStatsCount = enabledStats.Count(s => s.CheckType == HealthCheckTypes.Model);

            // 添加调试日志
            _logger.LogInformation($"Overview统计 - 启用分组数: {enabledGroups.Count}, 配置模型总数: {totalConfiguredModels}, 健康检查模型记录数: {modelStatsCount}, 健康模型: {healthyModels}, 异常模型: {unhealthyModels}");

            var overview = new
            {
                total_groups = enabledGroups.Count,
                healthy_providers = enabledStats.Count(s => s.CheckType == HealthCheckTypes.Provider && GetHealthStatus(s) == "healthy"),
                unhealthy_providers = enabledStats.Count(s => s.CheckType == HealthCheckTypes.Provider && GetHealthStatus(s) == "unhealthy"),

                total_keys = totalConfiguredKeys, // 使用配置的密钥总数
                healthy_keys = enabledStats.Count(s => s.CheckType == HealthCheckTypes.ApiKey && GetHealthStatus(s) == "healthy"),
                unhealthy_keys = enabledStats.Count(s => s.CheckType == HealthCheckTypes.ApiKey && GetHealthStatus(s) == "unhealthy"),

                total_models = totalConfiguredModels, // 使用配置的模型总数
                healthy_models = healthyModels,
                unhealthy_models = unhealthyModels,

                last_check = enabledStats.Count > 0 ? enabledStats.Max(s => s.LastCheckAt) : (DateTime?)null,
                overall_success_rate = enabledStats.Count > 0 ? enabledStats.Average(s => CalculateSuccessRate(s)) : 0
            };

            return Ok(new { success = true, data = overview });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取健康检查概览时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 调试端点：获取特定分组的详细统计信息
    /// </summary>
    [HttpGet("debug/{groupName}")]
    public async Task<IActionResult> GetGroupDebugInfo(string groupName)
    {
        try
        {
            var allStats = await _healthCheckService.GetAllHealthCheckStatsAsync();
            var groups = await _keyManager.GetAllGroupsAsync();
            var targetGroup = groups.FirstOrDefault(g => g.GroupName.Contains(groupName, StringComparison.OrdinalIgnoreCase));

            if (targetGroup == null)
            {
                return NotFound(new { success = false, error = $"未找到包含'{groupName}'的分组" });
            }

            var groupStats = allStats.Where(s => s.GroupId == targetGroup.Id).ToList();
            var modelStats = groupStats.Where(s => s.CheckType == HealthCheckTypes.Model).ToList();

            var debugInfo = new
            {
                group_info = new
                {
                    id = targetGroup.Id,
                    name = targetGroup.GroupName,
                    enabled = targetGroup.Enabled,
                    health_check_enabled = targetGroup.HealthCheckEnabled,
                    configured_models = GetModelCount(targetGroup),
                    configured_keys = GetApiKeyCount(targetGroup)
                },
                model_stats = modelStats.Select(s => new
                {
                    check_type = s.CheckType,
                    health_status = GetHealthStatus(s),
                    consecutive_failures = s.ConsecutiveFailures,
                    successful_checks = s.SuccessfulChecks,
                    total_checks = s.TotalChecks,
                    last_check = s.LastCheckAt,
                    last_success = s.LastSuccessAt,
                    last_failure = s.LastFailureAt,
                    success_rate = CalculateSuccessRate(s)
                }).ToList(),
                summary = new
                {
                    total_model_stats = modelStats.Count,
                    healthy_models = modelStats.Count(s => GetHealthStatus(s) == "healthy"),
                    unhealthy_models = modelStats.Count(s => GetHealthStatus(s) == "unhealthy"),
                    warning_models = modelStats.Count(s => GetHealthStatus(s) == "warning"),
                    unknown_models = modelStats.Count(s => GetHealthStatus(s) == "unknown")
                }
            };

            return Ok(new { success = true, data = debugInfo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取分组'{groupName}'调试信息时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    #region 私有辅助方法

    private string GetHealthStatus(HealthCheckStats? stats)
    {
        if (stats == null) return "unknown";
        
        if (stats.ConsecutiveFailures == 0 && stats.SuccessfulChecks > 0)
            return "healthy";
        
        if (stats.ConsecutiveFailures > 0 && stats.ConsecutiveFailures < 3)
            return "warning";
        
        return "unhealthy";
    }

    private double CalculateSuccessRate(HealthCheckStats? stats)
    {
        if (stats == null || stats.TotalChecks == 0) return 0;
        return (double)stats.SuccessfulChecks / stats.TotalChecks * 100;
    }

    private int GetApiKeyCount(GroupConfig group)
    {
        try
        {
            var apiKeys = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
            return apiKeys.Count;
        }
        catch
        {
            return 0;
        }
    }

    private int GetModelCount(GroupConfig group)
    {
        try
        {
            var models = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(group.Models) ?? new List<string>();
            return models.Count;
        }
        catch
        {
            return 0;
        }
    }



    /// <summary>
    /// 计算健康检查结果的成功率
    /// </summary>
    private double CalculateSuccessRateFromResults(List<HealthCheckResult> results)
    {
        if (results == null || results.Count == 0) return 0;

        var successCount = results.Count(r => r.IsSuccess);
        return (double)successCount / results.Count * 100;
    }

    /// <summary>
    /// 基于HealthCheckResult按ModelId统计模型健康状态
    /// </summary>
    private async Task<(int healthyModels, int unhealthyModels)> CalculateModelHealthStatsFromResults(List<string> groupIds)
    {
        try
        {
            // 获取这些分组的最近模型检查结果
            var recentModelResults = await _db.Queryable<HealthCheckResult>()
                .Where(r => groupIds.Contains(r.GroupId) && r.CheckType == HealthCheckTypes.Model)
                .OrderByDescending(r => r.CheckedAt)
                .Take(1000) // 限制查询数量，避免性能问题
                .ToListAsync();

            if (recentModelResults.Count == 0)
            {
                _logger.LogInformation("没有找到模型健康检查结果，返回0统计");
                return (0, 0);
            }

            // 按(GroupId, ModelId)分组，取每个模型的最新结果
            var modelHealthStatus = recentModelResults
                .GroupBy(r => new { r.GroupId, r.ModelId })
                .Select(g => new {
                    g.Key.GroupId,
                    g.Key.ModelId,
                    LatestResult = g.OrderByDescending(r => r.CheckedAt).First()
                })
                .ToList();

            var healthyCount = modelHealthStatus.Count(m => m.LatestResult.IsSuccess);
            var unhealthyCount = modelHealthStatus.Count(m => !m.LatestResult.IsSuccess);

            _logger.LogInformation("基于HealthCheckResult统计模型健康状态 - 总模型数: {TotalModels}, 健康: {HealthyCount}, 异常: {UnhealthyCount}",
                modelHealthStatus.Count, healthyCount, unhealthyCount);

            return (healthyCount, unhealthyCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算模型健康状态统计时发生异常");
            return (0, 0);
        }
    }

    /// <summary>
    /// 计算单个分组的模型健康状态统计
    /// </summary>
    private async Task<object> CalculateGroupModelHealthStats(GroupConfig group, List<HealthCheckStats> modelStats)
    {
        try
        {
            // 获取该分组的最近模型检查结果
            var recentModelResults = await _db.Queryable<HealthCheckResult>()
                .Where(r => r.GroupId == group.Id && r.CheckType == HealthCheckTypes.Model)
                .OrderByDescending(r => r.CheckedAt)
                .Take(100) // 限制查询数量
                .ToListAsync();

            if (recentModelResults.Count == 0)
            {
                // 如果没有检查结果，使用原来的逻辑
                return new
                {
                    total_models = GetModelCount(group),
                    healthy_models = modelStats.Count(s => GetHealthStatus(s) == "healthy"),
                    unhealthy_models = modelStats.Count(s => GetHealthStatus(s) == "unhealthy"),
                    last_check = modelStats.Count > 0 ? modelStats.Max(s => s.LastCheckAt) : (DateTime?)null,
                    avg_success_rate = modelStats.Count > 0 ? modelStats.Average(s => CalculateSuccessRate(s)) : 0
                };
            }

            // 按ModelId分组，取每个模型的最新结果
            var modelHealthStatus = recentModelResults
                .GroupBy(r => r.ModelId)
                .Select(g => new {
                    ModelId = g.Key,
                    LatestResult = g.OrderByDescending(r => r.CheckedAt).First()
                })
                .ToList();

            var healthyCount = modelHealthStatus.Count(m => m.LatestResult.IsSuccess);
            var unhealthyCount = modelHealthStatus.Count(m => !m.LatestResult.IsSuccess);

            return new
            {
                total_models = GetModelCount(group), // 使用配置的模型总数
                healthy_models = healthyCount,
                unhealthy_models = unhealthyCount,
                last_check = modelHealthStatus.Count > 0 ? modelHealthStatus.Max(m => m.LatestResult.CheckedAt) : (DateTime?)null,
                avg_success_rate = modelHealthStatus.Count > 0 ?
                    modelHealthStatus.Average(m => m.LatestResult.IsSuccess ? 100.0 : 0.0) : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算分组模型健康状态统计时发生异常: {GroupId}", group.Id);

            // 发生异常时回退到原来的逻辑
            return new
            {
                total_models = GetModelCount(group),
                healthy_models = modelStats.Count(s => GetHealthStatus(s) == "healthy"),
                unhealthy_models = modelStats.Count(s => GetHealthStatus(s) == "unhealthy"),
                last_check = modelStats.Count > 0 ? modelStats.Max(s => s.LastCheckAt) : (DateTime?)null,
                avg_success_rate = modelStats.Count > 0 ? modelStats.Average(s => CalculateSuccessRate(s)) : 0
            };
        }
    }

    #endregion
}

/// <summary>
/// 触发健康检查请求
/// </summary>
public class TriggerHealthCheckRequest
{
    /// <summary>
    /// 分组ID（可选，为空时检查所有分组）
    /// </summary>
    public string? GroupId { get; set; }
}
