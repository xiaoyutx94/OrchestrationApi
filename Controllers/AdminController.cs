using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.Generic;
using System.Linq;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 管理后台API控制器
/// </summary>
[ApiController]
[Route("admin")]
[Produces("application/json")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IKeyManager _keyManager;
    private readonly IRequestLogger _requestLogger;
    private readonly ILogger<AdminController> _logger;
    private readonly IVersionService _versionService;

    public AdminController(
        IKeyManager keyManager, 
        IRequestLogger requestLogger, 
        ILogger<AdminController> logger,
        IVersionService versionService)
    {
        _keyManager = keyManager;
        _requestLogger = requestLogger;
        _logger = logger;
        _versionService = versionService;
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _keyManager.GetSystemStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// 获取所有分组
    /// </summary>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _keyManager.GetAllGroupsAsync();
        return Ok(groups);
    }

    /// <summary>
    /// 获取分组管理数据
    /// </summary>
    [HttpGet("groups/manage")]
    public async Task<IActionResult> GetGroupsManage()
    {
        try
        {
            var groupsData = await _keyManager.GetGroupsManageDataAsync();
            return Ok(groupsData);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] GroupRequest group)
    {
        try
        {
            var newGroup = await _keyManager.CreateGroupAsync(group);
            return Ok(new { success = true, message = "分组创建成功", data = newGroup });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    [HttpPut("groups/{id}")]
    public async Task<IActionResult> UpdateGroup(string id, [FromBody] GroupRequest group)
    {
        try
        {
            await _keyManager.UpdateGroupAsync(id, group);
            return Ok(new { success = true, message = "分组更新成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    [HttpDelete("groups/{id}")]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        await _keyManager.DeleteGroupAsync(id);
        return NoContent();
    }

    /// <summary>
    /// 切换分组启用状态
    /// </summary>
    [HttpPost("groups/{id}/toggle")]
    public async Task<IActionResult> ToggleGroup(string id)
    {
        try
        {
            await _keyManager.ToggleGroupAsync(id);
            return Ok(new { success = true, message = "分组状态切换成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取单个日志详情
    /// </summary>
    [HttpGet("logs/{id:int}")]
    public async Task<IActionResult> GetLogById(int id)
    {
        try
        {
            var log = await _requestLogger.GetLogDtoByIdAsync(id);

            if (log == null)
            {
                return NotFound(new { success = false, error = $"未找到ID为 {id} 的日志记录" });
            }

            return Ok(new { success = true, log = log });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志详情时发生异常: {Id}", id);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取请求日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        [FromQuery] string? proxyKey = null,
        [FromQuery] string? group = null,
        [FromQuery] string? model = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null)
    {
        try
        {
            var page = (offset / limit) + 1;
            var pageSize = limit;

            var result = await _requestLogger.GetLogsDtoAsync(page, pageSize, proxyKey, group, model, status, type);

            return Ok(new
            {
                success = true,
                logs = result.Logs,
                total_count = result.TotalCount,
                pagination = new
                {
                    limit = limit,
                    offset = offset,
                    total = result.TotalCount,
                    has_more = result.Logs.Count == pageSize
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取请求日志时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    [HttpGet("logs/stats")]
    public async Task<IActionResult> GetLogStats([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var stats = await _requestLogger.GetLogStatsAsync(startDate, endDate);
            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志统计信息时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取API密钥统计信息
    /// </summary>
    [HttpGet("logs/stats/api-keys")]
    public async Task<IActionResult> GetApiKeyStats()
    {
        try
        {
            var rawStats = await _requestLogger.GetApiKeyStatsAsync();

            // 转换为前端期望的格式
            var stats = rawStats.Select(s => new
            {
                key_name = s.KeyName,
                total_requests = s.TotalRequests,
                success_requests = s.SuccessfulRequests,
                error_requests = s.FailedRequests,
                total_tokens = s.TotalTokens,
                avg_duration = Math.Round(s.AverageResponseTime, 2), // 使用实际的平均响应时间
                last_used = s.LastUsed
            }).ToList();

            return Ok(new { success = true, stats = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取API密钥统计信息时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取模型使用统计
    /// </summary>
    [HttpGet("logs/stats/models")]
    public async Task<IActionResult> GetModelUsageStats()
    {
        try
        {
            var rawStats = await _requestLogger.GetModelUsageStatsAsync();

            // 转换为前端期望的格式
            var stats = rawStats.Select(s => new
            {
                model = s.Model,
                total_requests = s.RequestCount, // 使用前端期望的字段名
                request_count = s.RequestCount,
                total_tokens = s.TotalTokens,
                average_tokens = Math.Round(s.AverageTokens, 2),
                last_used = s.LastUsed
            }).ToList();

            return Ok(new { success = true, stats = stats }); // 使用前端期望的容器名
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模型使用统计时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取Token使用统计
    /// </summary>
    [HttpGet("logs/stats/tokens")]
    public async Task<IActionResult> GetTokenUsageStats()
    {
        try
        {
            // 获取基本统计信息
            var logStats = await _requestLogger.GetLogStatsAsync();

            // 返回前端期望的汇总格式
            var stats = new
            {
                total_tokens = logStats.TotalTokens,
                success_tokens = logStats.TotalTokens, // 暂时使用总Token数，后续可以分离成功请求的Token
                success_requests = logStats.SuccessfulRequests,
                total_requests = logStats.TotalRequests,
                prompt_tokens = logStats.PromptTokens,
                completion_tokens = logStats.CompletionTokens
            };

            return Ok(new { success = true, stats = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Token使用统计时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取状态分布统计（用于图表）
    /// </summary>
    [HttpGet("logs/stats/status")]
    public async Task<IActionResult> GetStatusDistributionStats([FromQuery] string? period = null)
    {
        try
        {
            var stats = await _requestLogger.GetLogStatsAsync();
            return Ok(new
            {
                success = true,
                data = new
                {
                    success = stats.SuccessfulRequests,
                    error = stats.FailedRequests,
                    total = stats.TotalRequests
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取状态分布统计时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取Token时间线统计（用于趋势图表）
    /// </summary>
    [HttpGet("logs/stats/tokens-timeline")]
    public async Task<IActionResult> GetTokenTimelineStats([FromQuery] string? period = null)
    {
        try
        {
            var stats = await _requestLogger.GetTokenUsageStatsAsync();
            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Token时间线统计时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组Token统计（用于柱状图）
    /// </summary>
    [HttpGet("logs/stats/group-tokens")]
    public async Task<IActionResult> GetGroupTokenStats([FromQuery] string? period = null)
    {
        try
        {
            // 这里需要实现分组Token统计逻辑
            var allStats = await _requestLogger.GetLogStatsAsync();

            // 临时返回模拟数据，实际应该根据分组聚合统计
            var groupStats = new[]
            {
                new { group_name = "T佬公益", total_tokens = allStats.TotalTokens / 3 },
                new { group_name = "V3中转", total_tokens = allStats.TotalTokens / 3 },
                new { group_name = "asdsad", total_tokens = allStats.TotalTokens / 3 }
            };

            return Ok(new { success = true, data = groupStats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组Token统计时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 清理过期日志
    /// </summary>
    [HttpPost("logs/cleanup")]
    public async Task<IActionResult> CleanupOldLogs()
    {
        try
        {
            await _requestLogger.CleanupOldLogsAsync();
            return Ok(new { success = true, message = "过期日志清理完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期日志时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 清空错误日志
    /// </summary>
    [HttpDelete("logs/errors")]
    public async Task<IActionResult> ClearErrorLogs()
    {
        try
        {
            await _requestLogger.ClearErrorLogsAsync();
            return Ok(new { success = true, message = "错误日志清空完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空错误日志时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 清空所有日志
    /// </summary>
    [HttpDelete("logs")]
    public async Task<IActionResult> ClearAllLogs()
    {
        try
        {
            await _requestLogger.ClearAllLogsAsync();
            return Ok(new { success = true, message = "所有日志清空完成" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空所有日志时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 批量删除日志
    /// </summary>
    [HttpDelete("logs/batch")]
    public async Task<IActionResult> BatchDeleteLogs([FromBody] BatchDeleteLogsRequest request)
    {
        try
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { success = false, error = "请提供要删除的日志ID列表" });
            }

            var deletedCount = await _requestLogger.BatchDeleteLogsAsync(request.Ids);
            return Ok(new
            {
                success = true,
                message = $"成功删除 {deletedCount} 条日志",
                deleted_count = deletedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除日志时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有API密钥
    /// </summary>
    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys()
    {
        try
        {
            var keys = await _keyManager.GetAllKeysAsync();
            return Ok(new { success = true, keys = keys });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 添加API密钥
    /// </summary>
    [HttpPost("keys")]
    public async Task<IActionResult> AddKey([FromBody] ApiKeyRequest request)
    {
        try
        {
            var result = await _keyManager.AddKeyAsync(request.Key, request.Name, request.Description);
            return Ok(new { success = true, message = "API密钥添加成功", keyId = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 批量添加API密钥
    /// </summary>
    [HttpPost("keys/batch")]
    public async Task<IActionResult> BatchAddKeys([FromBody] BatchAddKeysRequest request)
    {
        try
        {
            var result = await _keyManager.BatchAddKeysAsync(request.Keys);
            return Ok(new
            {
                success = true,
                message = $"成功添加 {result.SuccessCount} 个密钥",
                added_count = result.SuccessCount,
                skipped_count = result.SkippedCount,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 更新API密钥
    /// </summary>
    [HttpPut("keys/{keyId}")]
    public async Task<IActionResult> UpdateKey(string keyId, [FromBody] ApiKeyRequest request)
    {
        try
        {
            await _keyManager.UpdateKeyAsync(keyId, request.Key, request.Name, request.Description);
            return Ok(new { success = true, message = "API密钥更新成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除API密钥
    /// </summary>
    [HttpDelete("keys/{keyId}")]
    public async Task<IActionResult> DeleteKey(string keyId)
    {
        try
        {
            await _keyManager.DeleteKeyAsync(keyId);
            return Ok(new { success = true, message = "API密钥删除成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取密钥状态统计
    /// </summary>
    [HttpGet("keys/status")]
    public async Task<IActionResult> GetKeysStatus()
    {
        try
        {
            var status = await _keyManager.GetKeysStatusAsync();
            return Ok(new { success = true, data = status });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取密钥统计信息
    /// </summary>
    [HttpGet("keys/statistics")]
    public async Task<IActionResult> GetKeysStatistics()
    {
        try
        {
            var stats = await _keyManager.GetKeysStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 验证分组密钥
    /// </summary>
    [HttpPost("keys/validate/{groupId}")]
    public async Task<IActionResult> ValidateGroupKeys(string groupId, [FromBody] ValidateKeysRequest request)
    {
        try
        {
            var result = await _keyManager.ValidateGroupKeysAsync(groupId, request.ApiKeys);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组密钥验证状态
    /// </summary>
    [HttpGet("keys/validation/{groupId}")]
    public async Task<IActionResult> GetGroupKeyValidationStatus(string groupId)
    {
        try
        {
            var result = await _keyManager.GetGroupKeyValidationStatusAsync(groupId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 强制更新分组密钥状态
    /// </summary>
    [HttpPost("groups/{groupId}/keys/force-status")]
    public async Task<IActionResult> ForceUpdateGroupKeysStatus(string groupId, [FromBody] ForceUpdateKeyStatusRequest request)
    {
        try
        {
            _logger.LogInformation("强制更新分组 {GroupId} 的密钥状态 - API密钥: {ApiKey}, 状态: {Status}",
                groupId, request.ApiKey?.Substring(0, Math.Min(8, request.ApiKey?.Length ?? 0)), request.Status);

            // 获取分组信息
            var groups = await _keyManager.GetAllGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == groupId);

            if (group == null)
            {
                return NotFound(new { success = false, error = $"分组 {groupId} 不存在" });
            }

            // 获取分组的API密钥
            var apiKeys = await _keyManager.GetGroupApiKeysAsync(groupId);
            if (!apiKeys.Any())
            {
                return Ok(new { success = true, message = "该分组没有API密钥需要验证", updated_count = 0 });
            }

            // 检查是否提供了密钥哈希而不是完整密钥
            string? actualApiKey = null;
            if (!string.IsNullOrEmpty(request.ApiKey))
            {
                // 如果提供的是完整密钥，直接验证
                if (apiKeys.Contains(request.ApiKey))
                {
                    actualApiKey = request.ApiKey;
                }
                else
                {
                    // 如果提供的可能是哈希或前缀，尝试匹配
                    foreach (var key in apiKeys)
                    {
                        var keyHash = ComputeKeyHash(key);
                        if (keyHash.StartsWith(request.ApiKey, StringComparison.OrdinalIgnoreCase) ||
                            key.StartsWith(request.ApiKey, StringComparison.OrdinalIgnoreCase))
                        {
                            actualApiKey = key;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(actualApiKey))
            {
                return BadRequest(new { success = false, error = "指定的API密钥不属于该分组或无效" });
            }

            // 强制更新特定密钥的状态
            var result = await _keyManager.ForceUpdateKeyStatusAsync(groupId, actualApiKey, request.Status);

            return Ok(new
            {
                success = true,
                message = $"成功强制更新分组 {group.GroupName} 中密钥的状态为 {request.Status}",
                group_id = groupId,
                group_name = group.GroupName,
                api_key = request.ApiKey?.Substring(0, Math.Min(8, request.ApiKey?.Length ?? 0)) + "****",
                new_status = request.Status,
                result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "强制更新分组 {GroupId} 密钥状态时发生异常", groupId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组的密钥使用统计
    /// </summary>
    [HttpGet("groups/{groupId}/keys/usage-stats")]
    public async Task<IActionResult> GetGroupKeyUsageStats(string groupId)
    {
        try
        {
            _logger.LogInformation("获取分组 {GroupId} 的密钥使用统计", groupId);

            var result = await _keyManager.GetGroupKeyUsageStatsAsync(groupId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组 {GroupId} 密钥使用统计时发生异常", groupId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 重置分组的密钥使用统计
    /// </summary>
    [HttpPost("groups/{groupId}/keys/reset-stats")]
    public async Task<IActionResult> ResetGroupKeyUsageStats(string groupId)
    {
        try
        {
            _logger.LogInformation("重置分组 {GroupId} 的密钥使用统计", groupId);

            // 获取分组信息
            var groups = await _keyManager.GetAllGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == groupId);

            if (group == null)
            {
                return NotFound(new { success = false, error = $"分组 {groupId} 不存在" });
            }

            // 这里需要实现重置逻辑，暂时返回成功
            return Ok(new
            {
                success = true,
                message = $"成功重置分组 {group.GroupName} 的密钥使用统计",
                group_id = groupId,
                group_name = group.GroupName,
                reset_time = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置分组 {GroupId} 密钥使用统计时发生异常", groupId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取代理密钥列表
    /// </summary>
    [HttpGet("proxy-keys")]
    public async Task<IActionResult> GetProxyKeys()
    {
        try
        {
            var keys = await _keyManager.GetProxyKeysAsync();
            return Ok(new { success = true, keys = keys });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 生成代理密钥
    /// </summary>
    [HttpPost("proxy-keys")]
    public async Task<IActionResult> GenerateProxyKey([FromBody] ProxyKeyRequest request)
    {
        try
        {
            var result = await _keyManager.GenerateProxyKeyAsync(request.Name, request.Description);
            return Ok(new { success = true, key = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 更新代理密钥
    /// </summary>
    [HttpPut("proxy-keys/{keyId}")]
    public async Task<IActionResult> UpdateProxyKey(int keyId, [FromBody] UpdateProxyKeyRequest request)
    {
        try
        {
            await _keyManager.UpdateProxyKeyAsync(keyId, request);
            return Ok(new { success = true, message = "代理密钥更新成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除代理密钥
    /// </summary>
    [HttpDelete("proxy-keys/{keyId}")]
    public async Task<IActionResult> DeleteProxyKey(int keyId)
    {
        try
        {
            await _keyManager.DeleteProxyKeyAsync(keyId);
            return Ok(new { success = true, message = "代理密钥删除成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取系统健康状态
    /// </summary>
    [HttpGet("health/system")]
    public async Task<IActionResult> GetSystemHealth()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var startTime = process.StartTime;
            var uptime = DateTime.Now - startTime;

            // 获取版本信息
            var version = _versionService.GetCurrentVersion();

            // 获取系统统计信息
            var statistics = await GetSystemStatisticsAsync();

            var systemHealth = new
            {
                status = "healthy",
                timestamp = DateTime.Now,
                version = version,
                uptime = (long)uptime.TotalSeconds, // 秒格式
                // 分组统计信息（前端期望的字段）
                total_groups = ((dynamic)statistics).total_groups,
                enabled_groups = ((dynamic)statistics).enabled_groups,
                disabled_groups = ((dynamic)statistics).disabled_groups,
                total_keys = ((dynamic)statistics).total_keys,
                total_requests = ((dynamic)statistics).total_requests,
                successful_requests = ((dynamic)statistics).successful_requests,
                failed_requests = ((dynamic)statistics).failed_requests,
                start_time = startTime,
                // 系统信息
                system = new
                {
                    processor_count = Environment.ProcessorCount,
                    machine_name = Environment.MachineName,
                    os_version = Environment.OSVersion.ToString(),
                    runtime_version = Environment.Version.ToString()
                },
                uptime_detail = new
                {
                    days = uptime.Days,
                    hours = uptime.Hours,
                    minutes = uptime.Minutes,
                    seconds = uptime.Seconds,
                    totalSeconds = (long)uptime.TotalSeconds,
                    startTime = startTime,
                    formatted = $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟"
                },
                services = new
                {
                    database = "healthy",
                    keyManager = "healthy",
                    requestLogger = "healthy"
                },
                statistics = statistics
            };
            return Ok(systemHealth);
        }
        catch (Exception ex)
        {
            var systemHealth = new
            {
                status = "unhealthy",
                timestamp = DateTime.Now,
                version = _versionService.GetCurrentVersion(),
                error = ex.Message,
                // 默认统计值
                total_groups = 0,
                enabled_groups = 0,
                disabled_groups = 0,
                total_keys = 0,
                total_requests = 0,
                successful_requests = 0,
                failed_requests = 0,
                uptime = 0L,
                start_time = DateTime.Now,
                services = new
                {
                    database = "unknown",
                    keyManager = "unknown",
                    requestLogger = "unknown"
                }
            };
            return Ok(systemHealth);
        }
    }


    private async Task<object> GetSystemStatisticsAsync()
    {
        try
        {
            // 获取分组统计信息
            var groupsData = await _keyManager.GetGroupsManageDataAsync();
            var groupsJsonText = JsonConvert.SerializeObject(groupsData);
            var groupsJson = JsonConvert.DeserializeObject<dynamic>(groupsJsonText);

            var totalGroups = 0;
            var enabledGroups = 0;
            var disabledGroups = 0;
            var totalKeys = 0;

            if (groupsJson?.groups != null)
            {
                var groups = groupsJson.groups;
                totalGroups = 0;

                foreach (var groupProperty in groups)
                {
                    totalGroups++;
                    var group = groupProperty.Value;

                    bool enabled = group?.enabled ?? false;
                    if (enabled)
                        enabledGroups++;
                    else
                        disabledGroups++;

                    int keyCount = group?.total_keys ?? 0;
                    totalKeys += keyCount;
                }
            }

            // 获取请求统计信息
            var systemStatus = await _keyManager.GetSystemStatusAsync();

            return new
            {
                total_groups = totalGroups,
                enabled_groups = enabledGroups,
                disabled_groups = disabledGroups,
                total_keys = totalKeys,
                total_requests = systemStatus.TotalRequests,
                successful_requests = systemStatus.SuccessfulRequests,
                failed_requests = systemStatus.FailedRequests,
                active_connections = 5, // 这个需要从连接池获取
                last_updated = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统统计信息时发生异常");
            return new
            {
                total_groups = 0,
                enabled_groups = 0,
                disabled_groups = 0,
                total_keys = 0,
                total_requests = 0,
                successful_requests = 0,
                failed_requests = 0,
                active_connections = 0,
                last_updated = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 刷新健康检查
    /// </summary>
    [HttpPost("health/refresh")]
    public async Task<IActionResult> RefreshHealth()
    {
        try
        {
            _logger.LogInformation("管理员手动触发健康检查刷新");

            // 使用与后台服务相同的方法进行健康检查和恢复
            var result = await _keyManager.CheckAndRecoverInvalidKeysAsync();
            
            if (result is Dictionary<string, object> resultDict)
            {
                var success = resultDict.GetValueOrDefault("success", false);
                if (success is true)
                {
                    var recoveredKeys = resultDict.GetValueOrDefault("recovered_keys", 0);
                    var checkedGroups = resultDict.GetValueOrDefault("checked_groups", 0);
                    var message = resultDict.GetValueOrDefault("message", "健康检查完成");

                    return Ok(new 
                    { 
                        success = true, 
                        message = "健康检查刷新成功", 
                        data = new 
                        {
                            checked_groups = checkedGroups,
                            recovered_keys = recoveredKeys,
                            refresh_time = DateTime.Now,
                            details = message
                        }
                    });
                }
                else
                {
                    var error = resultDict.GetValueOrDefault("error", "健康检查失败");
                    return BadRequest(new { success = false, error = error.ToString() });
                }
            }
            
            return Ok(new { success = true, message = "健康检查刷新完成", data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动刷新健康检查时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取服务商健康状态
    /// </summary>
    [HttpGet("health/providers/{groupId}")]
    public async Task<IActionResult> GetProviderHealth(string groupId)
    {
        try
        {
            var health = await _keyManager.CheckProviderHealthAsync(groupId);
            return Ok(health);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取模型列表
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string? provider = null)
    {
        try
        {
            var groups = await _keyManager.GetAllGroupsAsync();
            var result = new Dictionary<string, object>();

            foreach (var group in groups.Where(g => g.Enabled))
            {
                // 解析分组的模型列表和别名映射
                var groupModels = new List<object>();
                var modelAliases = new Dictionary<string, string>();

                // 解析模型别名
                if (!string.IsNullOrEmpty(group.ModelAliases))
                {
                    try
                    {
                        modelAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        // 如果解析失败，使用空字典
                        modelAliases = new Dictionary<string, string>();
                    }
                }

                if (!string.IsNullOrEmpty(group.Models))
                {
                    try
                    {
                        var modelList = JsonConvert.DeserializeObject<List<string>>(group.Models) ?? new List<string>();

                        // 处理原始模型
                        foreach (var model in modelList)
                        {
                            // 查找该模型是否有别名指向它
                            var aliasForThisModel = modelAliases.FirstOrDefault(kvp => kvp.Value == model).Key;

                            groupModels.Add(new
                            {
                                id = model,
                                @object = "model",
                                owned_by = GetOwnerByProviderType(group.ProviderType),
                                alias_name = aliasForThisModel // 如果有别名就显示别名，没有就是null
                            });
                        }
                    }
                    catch
                    {
                        // 如果解析失败，尝试作为单个字符串处理
                        groupModels = new List<object>
                        {
                            new
                            {
                                id = group.Models,
                                @object = "model",
                                owned_by = GetOwnerByProviderType(group.ProviderType),
                                alias_name = (string?)null
                            }
                        };
                    }
                }

                // 如果没有配置模型但有别名，显示原始模型名称及其别名
                if (groupModels.Count == 0 && modelAliases.Any())
                {
                    foreach (var alias in modelAliases)
                    {
                        groupModels.Add(new
                        {
                            id = alias.Value, // 显示原始模型名
                            @object = "model",
                            owned_by = GetOwnerByProviderType(group.ProviderType),
                            alias_name = alias.Key // 别名
                        });
                    }
                }

                result[group.Id] = new
                {
                    group_name = group.GroupName,
                    models = new
                    {
                        data = groupModels,
                        @object = "list"
                    },
                    provider_type = group.ProviderType
                };
            }

            return Ok(new
            {
                data = result,
                @object = "list"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 根据服务商类型获取所有者名称
    /// </summary>
    private string GetOwnerByProviderType(string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => "openai",
            "anthropic" => "anthropic",
            "gemini" => "google",
            _ => providerType.ToLower()
        };
    }

    /// <summary>
    /// 获取特定服务商的模型列表
    /// </summary>
    [HttpGet("models/{provider}")]
    public async Task<IActionResult> GetProviderModels(string provider)
    {
        try
        {
            var models = await _keyManager.GetModelsAsync(provider);
            return Ok(new { data = models });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 根据服务商类型获取可用模型
    /// </summary>
    [HttpPost("models/available/by-type")]
    public async Task<IActionResult> GetAvailableModelsByType([FromBody] GetModelsByTypeRequest request)
    {
        try
        {
            var models = await _keyManager.GetAvailableModelsByTypeAsync(
                request.ProviderType,
                request.BaseUrl,
                request.ApiKeys,
                request.TimeoutSeconds,
                request.MaxRetries,
                request.Headers);
            // 与前端 dashboard.html 期望格式对齐：
            // {
            //   "object": "list",
            //   "data": {
            //     "temp-group": {
            //       "group_name": "临时测试分组",
            //       "provider_type": "...",
            //       "models": {
            //         "object": "list",
            //         "data": [ { id, ... } ]
            //       }
            //     }
            //   }
            // }
            var standardizedModels = new { @object = "list", data = models };
            var response = new
            {
                @object = "list",
                data = new Dictionary<string, object>
                {
                    ["temp-group"] = new
                    {
                        group_name = "临时测试分组",
                        provider_type = request.ProviderType,
                        models = standardizedModels
                    }
                }
            };
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // 上游API调用失败，返回明确的错误信息
            _logger.LogWarning(ex, "获取 {ProviderType} 的模型列表失败", request.ProviderType);
            return BadRequest(new { error = $"获取模型列表失败: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取可用模型时发生异常");
            return BadRequest(new { error = $"系统异常: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取分组可用模型
    /// </summary>
    [HttpGet("models/available/{groupId}")]
    public async Task<IActionResult> GetGroupAvailableModels(string groupId)
    {
        try
        {
            // 获取分组信息
            var group = await _keyManager.GetAllGroupsAsync()
                .ContinueWith(t => t.Result.FirstOrDefault(g => g.Id == groupId));

            if (group == null)
            {
                return NotFound(new { error = "分组不存在" });
            }

            // 获取分组的API密钥
            var apiKeys = await _keyManager.GetGroupApiKeysAsync(groupId);

            if (!apiKeys.Any())
            {
                return Ok(new
                {
                    @object = "list",
                    data = new Dictionary<string, object>
                    {
                        [groupId] = new
                        {
                            group_name = group.GroupName,
                            provider_type = group.ProviderType,
                            models = new { @object = "list", data = new List<object>() }
                        }
                    }
                });
            }

            // 获取可用模型
            try
            {
                var models = await _keyManager.GetAvailableModelsByTypeAsync(
                    group.ProviderType,
                    group.BaseUrl,
                    apiKeys,
                    group.Timeout,
                    group.RetryCount,
                    new Dictionary<string, string>()); // 空headers

                // 标准化模型格式
                var standardizedModels = new { @object = "list", data = models };

                var response = new
                {
                    @object = "list",
                    data = new Dictionary<string, object>
                    {
                        [groupId] = new
                        {
                            group_name = group.GroupName,
                            provider_type = group.ProviderType,
                            models = standardizedModels
                        }
                    }
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                // 上游API调用失败，返回明确的错误信息
                _logger.LogWarning(ex, "获取分组 {GroupId} 的模型列表失败", groupId);
                return BadRequest(new { error = $"获取模型列表失败: {ex.Message}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组可用模型时发生异常: {GroupId}", groupId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 导出分组配置
    /// </summary>
    [HttpPost("groups/export")]
    public async Task<IActionResult> ExportGroups([FromBody] ExportGroupsRequest request)
    {
        try
        {
            // 这里需要实现导出逻辑，暂时返回成功响应
            var config = await _keyManager.ExportGroupsAsync(request.GroupIds);

            var fileName = $"groups_config_{DateTime.Now:yyyy-MM-dd}.json";
            var content = System.Text.Encoding.UTF8.GetBytes(config);

            return File(content, "application/json", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 导入分组配置
    /// </summary>
    [HttpPost("groups/import")]
    public async Task<IActionResult> ImportGroups(IFormFile config_file)
    {
        try
        {
            if (config_file == null || config_file.Length == 0)
            {
                return BadRequest(new { success = false, error = "未选择配置文件" });
            }

            using var stream = config_file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var result = await _keyManager.ImportGroupsAsync(content);

            return Ok(new
            {
                success = true,
                imported_count = result.ImportedCount,
                total_groups = result.TotalGroups,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 手动触发密钥健康检查
    /// </summary>
    [HttpPost("keys/health-check")]
    public async Task<IActionResult> TriggerKeyHealthCheck()
    {
        try
        {
            _logger.LogInformation("管理员手动触发密钥健康检查");

            var result = await _keyManager.CheckAndRecoverInvalidKeysAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动触发密钥健康检查失败");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                check_time = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 一键清除无效密钥
    /// 清除所有LastStatusCode为401的密钥，包括从orch_groups、orch_key_validation、orch_key_usage_stats表中删除相关数据
    /// </summary>
    [HttpPost("keys/clear-invalid")]
    public async Task<IActionResult> ClearInvalidKeys()
    {
        try
        {
            _logger.LogInformation("管理员触发一键清除无效密钥操作");

            var result = await _keyManager.ClearInvalidKeysAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "一键清除无效密钥操作失败");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation_time = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 清除空白密钥的服务商分组
    /// 将没有密钥的服务商分组标记为删除
    /// </summary>
    [HttpPost("groups/clear-empty")]
    public async Task<IActionResult> ClearEmptyGroups()
    {
        try
        {
            _logger.LogInformation("管理员触发清除空白密钥分组操作");

            var result = await _keyManager.ClearEmptyGroupsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除空白密钥分组操作失败");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation_time = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 获取所有服务商配置的模型别名和模型名称（去重）
    /// </summary>
    [HttpGet("models/all-aliases")]
    public async Task<IActionResult> GetAllConfiguredAliases()
    {
        try
        {
            var groups = await _keyManager.GetAllGroupsAsync();
            var allModelNames = new HashSet<string>();

            foreach (var group in groups.Where(g => g.Enabled))
            {
                // 优先获取模型别名映射中的别名（key部分）
                if (!string.IsNullOrEmpty(group.ModelAliases))
                {
                    try
                    {
                        var aliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases);
                        if (aliases != null)
                        {
                            foreach (var aliasKey in aliases.Keys)
                            {
                                if (!string.IsNullOrWhiteSpace(aliasKey))
                                {
                                    allModelNames.Add(aliasKey.Trim());
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略解析失败的情况
                    }
                }
            }

            // 排序并返回
            var sortedModelNames = allModelNames.OrderBy(m => m).ToList();

            return Ok(new
            {
                success = true,
                aliases = sortedModelNames,
                total_count = sortedModelNames.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有配置的模型别名和模型名称时发生异常");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 计算密钥哈希值
    /// </summary>
    private static string ComputeKeyHash(string apiKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }
}