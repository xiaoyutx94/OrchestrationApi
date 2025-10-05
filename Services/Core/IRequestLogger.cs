using OrchestrationApi.Models;
using OrchestrationApi.Configuration;
using SqlSugar;
using Microsoft.Extensions.Options;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 请求日志分页结果
/// </summary>
public class PagedLogsResult
{
    public List<RequestLog> Logs { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// 请求日志DTO分页结果
/// </summary>
public class PagedLogsDtoResult
{
    public List<LogResponseDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// 请求日志接口
/// </summary>
public interface IRequestLogger
{
    /// <summary>
    /// 记录请求日志
    /// </summary>
    Task LogRequestAsync(RequestLog requestLog);

    /// <summary>
    /// 记录请求开始（用于计算耗时）
    /// </summary>
    Task<string> LogRequestStartAsync(string method, string? endpoint, string? requestBody = null, 
        Dictionary<string, string>? headers = null, int? proxyKeyId = null, string? clientIp = null, string? userAgent = null);

    /// <summary>
    /// 完成请求日志记录（更新响应信息）
    /// </summary>
    Task LogRequestEndAsync(string requestId, int statusCode, string? responseBody = null, 
        Dictionary<string, string>? responseHeaders = null, string? errorMessage = null,
        int? promptTokens = null, int? completionTokens = null, int? totalTokens = null,
        string? groupId = null, string? providerType = null, string? model = null, bool hasTools = false, bool isStreaming = false, string? openrouterKey = null);

    /// <summary>
    /// 获取请求日志
    /// </summary>
    Task<PagedLogsResult> GetLogsAsync(int page = 1, int pageSize = 20, string? proxyKeyFilter = null, 
        string? groupFilter = null, string? modelFilter = null, string? statusFilter = null, string? typeFilter = null);

    /// <summary>
    /// 获取请求日志 - 返回前端格式DTO
    /// </summary>
    Task<PagedLogsDtoResult> GetLogsDtoAsync(int page = 1, int pageSize = 20, string? proxyKeyFilter = null, 
        string? groupFilter = null, string? modelFilter = null, string? statusFilter = null, string? typeFilter = null);

    /// <summary>
    /// 获取指定时间范围的日志
    /// </summary>
    Task<List<RequestLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate, int page = 1, int pageSize = 20);

    /// <summary>
    /// 获取指定分组的日志
    /// </summary>
    Task<List<RequestLog>> GetLogsByGroupAsync(string groupId, int page = 1, int pageSize = 20);

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    Task<RequestLogStats> GetLogStatsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取API密钥统计信息
    /// </summary>
    Task<List<ApiKeyStatistics>> GetApiKeyStatsAsync();

    /// <summary>
    /// 获取模型使用统计
    /// </summary>
    Task<List<ModelUsageStatistics>> GetModelUsageStatsAsync();

    /// <summary>
    /// 获取Token使用统计
    /// </summary>
    Task<List<TokenUsageStatistics>> GetTokenUsageStatsAsync();

    /// <summary>
    /// 清理过期日志
    /// </summary>
    Task CleanupOldLogsAsync();

    /// <summary>
    /// 清空错误日志
    /// </summary>
    Task ClearErrorLogsAsync();

    /// <summary>
    /// 清空所有日志
    /// </summary>
    Task ClearAllLogsAsync();

    /// <summary>
    /// 批量删除指定ID的日志
    /// </summary>
    Task<int> BatchDeleteLogsAsync(List<int> ids);

    /// <summary>
    /// 根据ID获取单个日志详情
    /// </summary>
    Task<RequestLog?> GetLogByIdAsync(int id);

    /// <summary>
    /// 根据ID获取单个日志详情DTO
    /// </summary>
    Task<LogResponseDto?> GetLogDtoByIdAsync(int id);
}

/// <summary>
/// 请求日志统计信息
/// </summary>
public class RequestLogStats
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
}

/// <summary>
/// API密钥统计信息
/// </summary>
public class ApiKeyStatistics
{
    public string KeyName { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public long TotalTokens { get; set; }
    public double AverageResponseTime { get; set; }
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// 模型使用统计信息
/// </summary>
public class ModelUsageStatistics
{
    public string Model { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public long TotalTokens { get; set; }
    public double AverageTokens { get; set; }
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// Token使用统计信息
/// </summary>
public class TokenUsageStatistics
{
    public DateTime Date { get; set; }
    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long RequestCount { get; set; }
}

/// <summary>
/// 请求日志实现
/// </summary>
public class RequestLogger : IRequestLogger
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<RequestLogger> _logger;
    private readonly RequestLoggingOptions _options;
    private readonly Dictionary<string, DateTime> _requestStartTimes = new();
    private readonly IServiceProvider _serviceProvider;

    public RequestLogger(ISqlSugarClient db, ILogger<RequestLogger> logger, IOptions<RequestLoggingOptions> options, IServiceProvider serviceProvider)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task LogRequestAsync(RequestLog requestLog)
    {
        if (!_options.Enabled) return;

        // 如果启用了异步队列，则加入队列
        if (_options.Queue.Enabled)
        {
            var queueItem = new LogQueueItem
            {
                Type = LogQueueItemType.Insert,
                RequestId = requestLog.RequestId,
                LogData = requestLog
            };

            var logProcessingService = _serviceProvider.GetService<OrchestrationApi.Services.Background.AsyncLogProcessingService>();
            if (logProcessingService != null)
            {
                var success = logProcessingService.EnqueueLog(queueItem);
                if (!success)
                {
                    _logger.LogWarning("无法将日志加入队列，回退到同步模式: {RequestId}", requestLog.RequestId);
                    await LogRequestSyncAsync(requestLog);
                }
                return;
            }
        }

        // 同步模式或队列服务不可用时的回退
        await LogRequestSyncAsync(requestLog);
    }

    /// <summary>
    /// 同步记录请求日志
    /// </summary>
    private async Task LogRequestSyncAsync(RequestLog requestLog)
    {
        try
        {
            await _db.Insertable(requestLog).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录请求日志时发生异常");
        }
    }

    public async Task<string> LogRequestStartAsync(string method, string? endpoint, string? requestBody = null,
        Dictionary<string, string>? headers = null, int? proxyKeyId = null, string? clientIp = null, string? userAgent = null)
    {
        if (!_options.Enabled) return Guid.NewGuid().ToString();

        // 排除健康检查请求
        if (_options.ExcludeHealthChecks && !string.IsNullOrEmpty(endpoint) && (endpoint.Contains("/health") || endpoint.Contains("/ping")))
        {
            return Guid.NewGuid().ToString();
        }

        var requestId = Guid.NewGuid().ToString();
        _requestStartTimes[requestId] = DateTime.Now;

        try
        {
            var requestLog = new RequestLog
            {
                RequestId = requestId,
                ProxyKeyId = proxyKeyId,
                Method = method,
                Endpoint = endpoint ?? "unknown",
                ClientIp = clientIp,
                UserAgent = userAgent,
                CreatedAt = DateTime.Now
            };

            // 根据配置决定是否记录详细内容
            if (_options.EnableDetailedContent)
            {
                requestLog.RequestBody = TruncateContent(requestBody, out bool truncated);
                requestLog.RequestHeaders = TruncateContent(headers != null ? 
                    string.Join(";", headers.Select(h => $"{h.Key}:{h.Value}")) : null, out bool headerTruncated);
                requestLog.ContentTruncated = truncated || headerTruncated;
            }

            // 如果启用了异步队列，则加入队列
            if (_options.Queue.Enabled)
            {
                var queueItem = new LogQueueItem
                {
                    Type = LogQueueItemType.Insert,
                    RequestId = requestId,
                    LogData = requestLog
                };

                var logProcessingService = _serviceProvider.GetService<OrchestrationApi.Services.Background.AsyncLogProcessingService>();
                if (logProcessingService != null)
                {
                    var success = logProcessingService.EnqueueLog(queueItem);
                    if (!success)
                    {
                        _logger.LogWarning("无法将日志加入队列，回退到同步模式: {RequestId}", requestId);
                        await _db.Insertable(requestLog).ExecuteCommandAsync();
                    }
                }
                else
                {
                    // 队列服务不可用，回退到同步模式
                    await _db.Insertable(requestLog).ExecuteCommandAsync();
                }
            }
            else
            {
                // 同步模式
                await _db.Insertable(requestLog).ExecuteCommandAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录请求开始日志时发生异常: {RequestId}", requestId);
        }

        return requestId;
    }

    public async Task LogRequestEndAsync(string requestId, int statusCode, string? responseBody = null,
        Dictionary<string, string>? responseHeaders = null, string? errorMessage = null,
        int? promptTokens = null, int? completionTokens = null, int? totalTokens = null,
        string? groupId = null, string? providerType = null, string? model = null, bool hasTools = false, bool isStreaming = false, string? openrouterKey = null)
    {
        if (!_options.Enabled) return;

        try
        {
            var durationMs = 0L;
            if (_requestStartTimes.TryGetValue(requestId, out var startTime))
            {
                durationMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
                _requestStartTimes.Remove(requestId);
            }

            string? responseBodyToSave = null;
            string? responseHeadersToSave = null;
            bool contentTruncated = false;

            if (_options.EnableDetailedContent)
            {
                responseBodyToSave = TruncateContent(responseBody, out bool responseTruncated);
                responseHeadersToSave = TruncateContent(responseHeaders != null ?
                    string.Join(";", responseHeaders.Select(h => $"{h.Key}:{h.Value}")) : null, out bool headerTruncated);
                contentTruncated = responseTruncated || headerTruncated;
            }

            // 如果启用了异步队列，则加入队列
            if (_options.Queue.Enabled)
            {
                var updateData = new LogUpdateData
                {
                    StatusCode = statusCode,
                    DurationMs = durationMs,
                    GroupId = groupId,
                    ProviderType = providerType,
                    Model = model,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    ErrorMessage = errorMessage,
                    HasTools = hasTools,
                    IsStreaming = isStreaming,
                    ResponseBody = responseBodyToSave,
                    ResponseHeaders = responseHeadersToSave,
                    ContentTruncated = contentTruncated,
                    OpenrouterKey = MaskApiKey(openrouterKey)
                };

                var queueItem = new LogQueueItem
                {
                    Type = LogQueueItemType.Update,
                    RequestId = requestId,
                    UpdateData = updateData
                };

                var logProcessingService = _serviceProvider.GetService<OrchestrationApi.Services.Background.AsyncLogProcessingService>();
                if (logProcessingService != null)
                {
                    var success = logProcessingService.EnqueueLog(queueItem);
                    if (!success)
                    {
                        _logger.LogWarning("无法将更新日志加入队列，回退到同步模式: {RequestId}", requestId);
                        await LogRequestEndSyncAsync(requestId, statusCode, durationMs, groupId, providerType, model,
                            promptTokens, completionTokens, totalTokens, errorMessage, hasTools, isStreaming,
                            responseBodyToSave, responseHeadersToSave, contentTruncated, MaskApiKey(openrouterKey));
                    }
                }
                else
                {
                    // 队列服务不可用，回退到同步模式
                    await LogRequestEndSyncAsync(requestId, statusCode, durationMs, groupId, providerType, model,
                        promptTokens, completionTokens, totalTokens, errorMessage, hasTools, isStreaming,
                        responseBodyToSave, responseHeadersToSave, contentTruncated, MaskApiKey(openrouterKey));
                }
            }
            else
            {
                // 同步模式
                await LogRequestEndSyncAsync(requestId, statusCode, durationMs, groupId, providerType, model,
                    promptTokens, completionTokens, totalTokens, errorMessage, hasTools, isStreaming,
                    responseBodyToSave, responseHeadersToSave, contentTruncated, MaskApiKey(openrouterKey));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成请求日志记录时发生异常: {RequestId}", requestId);
        }
    }

    /// <summary>
    /// 同步完成请求日志记录
    /// </summary>
    private async Task LogRequestEndSyncAsync(string requestId, int statusCode, long durationMs,
        string? groupId, string? providerType, string? model, int? promptTokens, int? completionTokens,
        int? totalTokens, string? errorMessage, bool hasTools, bool isStreaming,
        string? responseBody, string? responseHeaders, bool contentTruncated, string? openrouterKey)
    {
        // 获取现有日志记录以检查是否已截断
        var existingLog = await _db.Queryable<RequestLog>()
            .Where(rl => rl.RequestId == requestId)
            .FirstAsync();

        // 合并截断状态
        var finalContentTruncated = (existingLog?.ContentTruncated ?? false) || contentTruncated;

        await _db.Updateable<RequestLog>()
            .SetColumns(it => new RequestLog
            {
                StatusCode = statusCode,
                DurationMs = durationMs,
                GroupId = groupId,
                ProviderType = providerType,
                Model = model,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                ErrorMessage = errorMessage,
                HasTools = hasTools,
                IsStreaming = isStreaming,
                ResponseBody = responseBody,
                ResponseHeaders = responseHeaders,
                ContentTruncated = finalContentTruncated,
                OpenrouterKey = openrouterKey
            })
            .Where(rl => rl.RequestId == requestId)
            .ExecuteCommandAsync();
    }

    public async Task<PagedLogsResult> GetLogsAsync(int page = 1, int pageSize = 20, string? proxyKeyFilter = null,
        string? groupFilter = null, string? modelFilter = null, string? statusFilter = null, string? typeFilter = null)
    {
        try
        {
            var query = _db.Queryable<RequestLog>();

            if (!string.IsNullOrEmpty(proxyKeyFilter) && proxyKeyFilter != "所有密钥")
            {
                if (int.TryParse(proxyKeyFilter, out int keyId))
                {
                    query = query.Where(rl => rl.ProxyKeyId == keyId);
                }
            }

            if (!string.IsNullOrEmpty(groupFilter) && groupFilter != "所有分组")
            {
                query = query.Where(rl => rl.GroupId == groupFilter);
            }

            if (!string.IsNullOrEmpty(modelFilter) && modelFilter != "所有模型")
            {
                query = query.Where(rl => rl.Model == modelFilter);
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "所有状态")
            {
                if (statusFilter == "成功 (200)")
                {
                    query = query.Where(rl => rl.StatusCode >= 200 && rl.StatusCode < 300);
                }
                else if (statusFilter == "错误 (非200)")
                {
                    query = query.Where(rl => rl.StatusCode < 200 || rl.StatusCode >= 300);
                }
            }

            if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "所有类型")
            {
                if (typeFilter == "流式")
                {
                    query = query.Where(rl => rl.IsStreaming);
                }
                else if (typeFilter == "非流式")
                {
                    query = query.Where(rl => !rl.IsStreaming);
                }
            }

            // 获取总数
            var totalCount = await query.CountAsync();

            // 获取分页数据
            var logs = await query
                .OrderByDescending(rl => rl.CreatedAt)
                .ToPageListAsync(page, pageSize);

            return new PagedLogsResult
            {
                Logs = logs,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取请求日志时发生异常");
            return new PagedLogsResult
            {
                Logs = new List<RequestLog>(),
                TotalCount = 0
            };
        }
    }

    public async Task<PagedLogsDtoResult> GetLogsDtoAsync(int page = 1, int pageSize = 20, string? proxyKeyFilter = null,
        string? groupFilter = null, string? modelFilter = null, string? statusFilter = null, string? typeFilter = null)
    {
        try
        {
            var query = _db.Queryable<RequestLog>()
                .LeftJoin<ProxyKey>((rl, pk) => rl.ProxyKeyId == pk.Id)
                .LeftJoin<GroupConfig>((rl, pk, gc) => rl.GroupId == gc.Id);

            if (!string.IsNullOrEmpty(proxyKeyFilter) && proxyKeyFilter != "所有密钥")
            {
                // 前端发送的是密钥名称字符串，使用JOIN的ProxyKey表匹配KeyName
                query = query.Where((rl, pk, gc) => pk.KeyName == proxyKeyFilter);
            }

            if (!string.IsNullOrEmpty(groupFilter) && groupFilter != "所有分组")
            {
                // 解析格式化的分组字符串: "ProviderType (GroupId)"
                // 例如: "openai_responses (packycode_rp)" -> ProviderType="openai_responses", GroupId="packycode_rp"
                var openParenIndex = groupFilter.IndexOf(" (");
                var closeParenIndex = groupFilter.LastIndexOf(')');

                if (openParenIndex > 0 && closeParenIndex > openParenIndex)
                {
                    var providerType = groupFilter.Substring(0, openParenIndex);
                    var groupId = groupFilter.Substring(openParenIndex + 2, closeParenIndex - openParenIndex - 2);

                    query = query.Where((rl, pk, gc) =>
                        (rl.ProviderType == providerType || (string.IsNullOrEmpty(rl.ProviderType) && providerType == "未知")) &&
                        (rl.GroupId == groupId || (string.IsNullOrEmpty(rl.GroupId) && groupId == "无分组")));
                }
                else
                {
                    // 如果格式不匹配，回退到直接匹配GroupId
                    query = query.Where((rl, pk, gc) => rl.GroupId == groupFilter);
                }
            }

            if (!string.IsNullOrEmpty(modelFilter) && modelFilter != "所有模型")
            {
                query = query.Where((rl, pk, gc) => rl.Model == modelFilter);
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "所有状态")
            {
                // 前端发送的是 "200" 或 "error"
                if (statusFilter == "200")
                {
                    query = query.Where((rl, pk, gc) => rl.StatusCode >= 200 && rl.StatusCode < 300);
                }
                else if (statusFilter == "error")
                {
                    query = query.Where((rl, pk, gc) => rl.StatusCode < 200 || rl.StatusCode >= 300);
                }
            }

            if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "所有类型")
            {
                // 前端发送的是 "true" 或 "false" 字符串
                if (typeFilter == "true")
                {
                    query = query.Where((rl, pk, gc) => rl.IsStreaming);
                }
                else if (typeFilter == "false")
                {
                    query = query.Where((rl, pk, gc) => !rl.IsStreaming);
                }
            }

            // 获取总数
            var totalCount = await query.CountAsync();

            // 获取分页数据并转换为DTO
            var logs = await query
                .OrderByDescending((rl, pk, gc) => rl.CreatedAt)
                .Select((rl, pk, gc) => new LogResponseDto
                {
                    Id = rl.Id,
                    RequestId = rl.RequestId,
                    ProxyKeyName = pk.KeyName,
                    ProxyKeyId = rl.ProxyKeyId,
                    ProviderGroup = $"{rl.ProviderType ?? "未知"} ({rl.GroupId ?? "无分组"})",
                    Model = rl.Model,
                    StatusCode = rl.StatusCode,
                    Duration = rl.DurationMs,
                    TokensUsed = rl.TotalTokens,
                    ClientIp = rl.ClientIp,
                    OpenrouterKey = rl.OpenrouterKey,
                    Error = rl.ErrorMessage,
                    IsStream = rl.IsStreaming,
                    CreatedAt = rl.CreatedAt,
                    RequestBody = rl.RequestBody,
                    ResponseBody = rl.ResponseBody,
                    RequestHeaders = rl.RequestHeaders,
                    ResponseHeaders = rl.ResponseHeaders,
                    PromptTokens = rl.PromptTokens,
                    CompletionTokens = rl.CompletionTokens,
                    TotalTokens = rl.TotalTokens,
                    HasTools = rl.HasTools,
                    ContentTruncated = rl.ContentTruncated
                })
                .ToPageListAsync(page, pageSize);

            return new PagedLogsDtoResult
            {
                Logs = logs,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取请求日志DTO时发生异常");
            return new PagedLogsDtoResult
            {
                Logs = new List<LogResponseDto>(),
                TotalCount = 0
            };
        }
    }

    public async Task<List<RequestLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate, int page = 1, int pageSize = 20)
    {
        try
        {
            return await _db.Queryable<RequestLog>()
                .Where(rl => rl.CreatedAt >= startDate && rl.CreatedAt <= endDate)
                .OrderByDescending(rl => rl.CreatedAt)
                .ToPageListAsync(page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取指定时间范围的日志时发生异常");
            return new List<RequestLog>();
        }
    }

    public async Task<List<RequestLog>> GetLogsByGroupAsync(string groupId, int page = 1, int pageSize = 20)
    {
        try
        {
            return await _db.Queryable<RequestLog>()
                .Where(rl => rl.GroupId == groupId)
                .OrderByDescending(rl => rl.CreatedAt)
                .ToPageListAsync(page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取指定分组的日志时发生异常");
            return new List<RequestLog>();
        }
    }

    public async Task<RequestLogStats> GetLogStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _db.Queryable<RequestLog>();

            if (startDate.HasValue)
                query = query.Where(rl => rl.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(rl => rl.CreatedAt <= endDate.Value);

            var totalRequests = await query.CountAsync();
            var successfulRequests = await query.Where(rl => rl.StatusCode >= 200 && rl.StatusCode < 300).CountAsync();
            var failedRequests = totalRequests - successfulRequests;

            var avgResponseTime = totalRequests > 0 ? 
                await query.SumAsync(rl => rl.DurationMs) / (double)totalRequests : 0;
            var totalTokens = await query.SumAsync(rl => rl.TotalTokens ?? 0);
            var promptTokens = await query.SumAsync(rl => rl.PromptTokens ?? 0);
            var completionTokens = await query.SumAsync(rl => rl.CompletionTokens ?? 0);

            return new RequestLogStats
            {
                TotalRequests = totalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                AverageResponseTime = avgResponseTime,
                TotalTokens = totalTokens,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志统计信息时发生异常");
            return new RequestLogStats();
        }
    }

    public async Task<List<ApiKeyStatistics>> GetApiKeyStatsAsync()
    {
        try
        {
            // 简化实现：先获取原始数据再分组统计
            var rawData = await _db.Queryable<RequestLog>()
                .LeftJoin<ProxyKey>((rl, pk) => rl.ProxyKeyId == pk.Id)
                .Where((rl, pk) => rl.ProxyKeyId != null)
                .Select((rl, pk) => new
                {
                    KeyName = pk.KeyName,
                    StatusCode = rl.StatusCode,
                    TotalTokens = rl.TotalTokens ?? 0,
                    DurationMs = rl.DurationMs,
                    CreatedAt = rl.CreatedAt
                })
                .ToListAsync();

            var stats = rawData
                .GroupBy(r => r.KeyName)
                .Select(g => new ApiKeyStatistics
                {
                    KeyName = g.Key,
                    TotalRequests = g.Count(),
                    SuccessfulRequests = g.Count(r => r.StatusCode >= 200 && r.StatusCode < 300),
                    FailedRequests = g.Count(r => r.StatusCode < 200 || r.StatusCode >= 300),
                    TotalTokens = g.Sum(r => r.TotalTokens),
                    AverageResponseTime = g.Count() > 0 ? g.Average(r => r.DurationMs) : 0,
                    LastUsed = g.Max(r => (DateTime?)r.CreatedAt)
                })
                .ToList();

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取API密钥统计信息时发生异常");
            return new List<ApiKeyStatistics>();
        }
    }

    public async Task<List<ModelUsageStatistics>> GetModelUsageStatsAsync()
    {
        try
        {
            var rawData = await _db.Queryable<RequestLog>()
                .Where(rl => !string.IsNullOrEmpty(rl.Model))
                .Select(rl => new
                {
                    Model = rl.Model!,
                    TotalTokens = rl.TotalTokens ?? 0,
                    CreatedAt = rl.CreatedAt
                })
                .ToListAsync();

            var stats = rawData
                .GroupBy(r => r.Model)
                .Select(g => new ModelUsageStatistics
                {
                    Model = g.Key,
                    RequestCount = g.Count(),
                    TotalTokens = g.Sum(r => r.TotalTokens),
                    AverageTokens = g.Count() > 0 ? g.Average(r => r.TotalTokens) : 0,
                    LastUsed = g.Max(r => (DateTime?)r.CreatedAt)
                })
                .ToList();

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模型使用统计时发生异常");
            return new List<ModelUsageStatistics>();
        }
    }

    public async Task<List<TokenUsageStatistics>> GetTokenUsageStatsAsync()
    {
        try
        {
            var rawData = await _db.Queryable<RequestLog>()
                .Select(rl => new
                {
                    Date = rl.CreatedAt.Date,
                    TotalTokens = rl.TotalTokens ?? 0,
                    PromptTokens = rl.PromptTokens ?? 0,
                    CompletionTokens = rl.CompletionTokens ?? 0
                })
                .ToListAsync();

            var stats = rawData
                .GroupBy(r => r.Date)
                .Select(g => new TokenUsageStatistics
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(r => r.TotalTokens),
                    PromptTokens = g.Sum(r => r.PromptTokens),
                    CompletionTokens = g.Sum(r => r.CompletionTokens),
                    RequestCount = g.Count()
                })
                .OrderBy(ts => ts.Date)
                .ToList();

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Token使用统计时发生异常");
            return new List<TokenUsageStatistics>();
        }
    }

    public async Task CleanupOldLogsAsync()
    {
        if (_options.RetentionDays <= 0) return;

        try
        {
            var cutoffDate = DateTime.Now.AddDays(-_options.RetentionDays);
            await _db.Deleteable<RequestLog>()
                .Where(rl => rl.CreatedAt < cutoffDate)
                .ExecuteCommandAsync();

            _logger.LogInformation("已清理 {Days} 天前的旧日志记录", _options.RetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期日志时发生异常");
        }
    }

    public async Task ClearErrorLogsAsync()
    {
        try
        {
            await _db.Deleteable<RequestLog>()
                .Where(rl => rl.StatusCode < 200 || rl.StatusCode >= 300)
                .ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空错误日志时发生异常");
        }
    }

    public async Task ClearAllLogsAsync()
    {
        try
        {
            await _db.Deleteable<RequestLog>().ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空所有日志时发生异常");
        }
    }

    public async Task<int> BatchDeleteLogsAsync(List<int> ids)
    {
        try
        {
            if (ids == null || !ids.Any())
            {
                return 0;
            }

            var deletedCount = await _db.Deleteable<RequestLog>()
                .Where(rl => ids.Contains(rl.Id))
                .ExecuteCommandAsync();

            _logger.LogInformation("批量删除了 {Count} 条日志记录", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除日志时发生异常，尝试删除的ID: {Ids}", string.Join(", ", ids));
            throw;
        }
    }

    public async Task<RequestLog?> GetLogByIdAsync(int id)
    {
        try
        {
            return await _db.Queryable<RequestLog>()
                .Where(rl => rl.Id == id)
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据ID获取日志详情时发生异常: {Id}", id);
            return null;
        }
    }

    public async Task<LogResponseDto?> GetLogDtoByIdAsync(int id)
    {
        try
        {
            return await _db.Queryable<RequestLog>()
                .LeftJoin<ProxyKey>((rl, pk) => rl.ProxyKeyId == pk.Id)
                .LeftJoin<GroupConfig>((rl, pk, gc) => rl.GroupId == gc.Id)
                .Where((rl, pk, gc) => rl.Id == id)
                .Select((rl, pk, gc) => new LogResponseDto
                {
                    Id = rl.Id,
                    RequestId = rl.RequestId,
                    ProxyKeyName = pk.KeyName,
                    ProxyKeyId = rl.ProxyKeyId,
                    ProviderGroup = $"{rl.ProviderType ?? "未知"} ({rl.GroupId ?? "无分组"})",
                    Model = rl.Model,
                    StatusCode = rl.StatusCode,
                    Duration = rl.DurationMs,
                    TokensUsed = rl.TotalTokens,
                    ClientIp = rl.ClientIp,
                    OpenrouterKey = rl.OpenrouterKey,
                    Error = rl.ErrorMessage,
                    IsStream = rl.IsStreaming,
                    CreatedAt = rl.CreatedAt,
                    RequestBody = rl.RequestBody,
                    ResponseBody = rl.ResponseBody,
                    RequestHeaders = rl.RequestHeaders,
                    ResponseHeaders = rl.ResponseHeaders,
                    PromptTokens = rl.PromptTokens,
                    CompletionTokens = rl.CompletionTokens,
                    TotalTokens = rl.TotalTokens,
                    HasTools = rl.HasTools,
                    ContentTruncated = rl.ContentTruncated
                })
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据ID获取日志详情DTO时发生异常: {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// 脱敏API密钥 - 只保留前4位和后4位
    /// </summary>
    private string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;
            
        if (apiKey.Length <= 8)
            return new string('*', apiKey.Length);
            
        var prefix = apiKey.Substring(0, 4);
        var suffix = apiKey.Substring(apiKey.Length - 4);
        var maskLength = apiKey.Length - 8;
        var mask = new string('*', maskLength);
        
        return $"{prefix}{mask}{suffix}";
    }

    /// <summary>
    /// 截断内容到指定长度
    /// </summary>
    private string? TruncateContent(string? content, out bool truncated)
    {
        truncated = false;
        if (string.IsNullOrEmpty(content)) return content;

        if (content.Length > _options.MaxContentLength)
        {
            truncated = true;
            return content.Substring(0, _options.MaxContentLength) + "... [TRUNCATED]";
        }

        return content;
    }
}