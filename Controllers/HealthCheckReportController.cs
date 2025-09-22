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

            foreach (var group in groups.Where(g => g.Enabled))
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
                        total_keys = GetApiKeyCount(group),
                        healthy_keys = keyStats.Count(s => s.ConsecutiveFailures == 0),
                        unhealthy_keys = keyStats.Count(s => s.ConsecutiveFailures > 0),
                        last_check = keyStats.Max(s => s.LastCheckAt),
                        avg_success_rate = keyStats.Count > 0 ? keyStats.Average(s => CalculateSuccessRate(s)) : 0
                    },
                    
                    // 模型健康状态汇总
                    models_health = new
                    {
                        total_models = GetModelCount(group),
                        healthy_models = modelStats.Count(s => s.ConsecutiveFailures == 0),
                        unhealthy_models = modelStats.Count(s => s.ConsecutiveFailures > 0),
                        last_check = modelStats.Max(s => s.LastCheckAt),
                        avg_success_rate = modelStats.Count > 0 ? modelStats.Average(s => CalculateSuccessRate(s)) : 0
                    }
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
                    success_rate = CalculateSuccessRate(providerStats)
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
                    last_check = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.CheckedAt
                }).ToList(),

                // 模型健康状态统计
                models_health = new
                {
                    total_models = modelStatsGroup.Count,
                    healthy_models = modelStatsGroup.Count(s => GetHealthStatus(s) == "healthy")
                },

                // 模型详细信息
                models = modelResults.GroupBy(r => r.ModelId).Select(g => new
                {
                    model_name = g.Key,
                    health_status = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.IsSuccess == true ? "healthy" : "unhealthy",
                    success_rate = CalculateSuccessRateFromResults(g.ToList()),
                    avg_response_time = g.Where(r => r.ResponseTimeMs > 0).Average(r => (double)r.ResponseTimeMs),
                    last_check = g.OrderByDescending(r => r.CheckedAt).FirstOrDefault()?.CheckedAt
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
            var enabledGroups = groups.Where(g => g.Enabled).ToList();

            var overview = new
            {
                total_groups = enabledGroups.Count,
                healthy_providers = allStats.Count(s => s.CheckType == HealthCheckTypes.Provider && s.ConsecutiveFailures == 0),
                unhealthy_providers = allStats.Count(s => s.CheckType == HealthCheckTypes.Provider && s.ConsecutiveFailures > 0),
                
                total_keys = enabledGroups.Sum(g => GetApiKeyCount(g)),
                healthy_keys = allStats.Count(s => s.CheckType == HealthCheckTypes.ApiKey && s.ConsecutiveFailures == 0),
                unhealthy_keys = allStats.Count(s => s.CheckType == HealthCheckTypes.ApiKey && s.ConsecutiveFailures > 0),
                
                total_models = enabledGroups.Sum(g => GetModelCount(g)),
                healthy_models = allStats.Count(s => s.CheckType == HealthCheckTypes.Model && s.ConsecutiveFailures == 0),
                unhealthy_models = allStats.Count(s => s.CheckType == HealthCheckTypes.Model && s.ConsecutiveFailures > 0),
                
                last_check = allStats.Max(s => s.LastCheckAt),
                overall_success_rate = allStats.Count > 0 ? allStats.Average(s => CalculateSuccessRate(s)) : 0
            };

            return Ok(new { success = true, data = overview });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取健康检查概览时发生异常");
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
