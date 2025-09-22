using OrchestrationApi.Models;

namespace OrchestrationApi.Models;

/// <summary>
/// 日志队列项类型
/// </summary>
public enum LogQueueItemType
{
    /// <summary>
    /// 新建日志记录
    /// </summary>
    Insert,
    
    /// <summary>
    /// 更新日志记录
    /// </summary>
    Update
}

/// <summary>
/// 日志队列项 - 用于异步日志处理队列
/// </summary>
public class LogQueueItem
{
    /// <summary>
    /// 队列项类型
    /// </summary>
    public LogQueueItemType Type { get; set; }
    
    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
    
    /// <summary>
    /// 日志数据（用于Insert操作）
    /// </summary>
    public RequestLog? LogData { get; set; }
    
    /// <summary>
    /// 更新数据（用于Update操作）
    /// </summary>
    public LogUpdateData? UpdateData { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// 最后错误信息
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// 日志更新数据 - 用于更新操作
/// </summary>
public class LogUpdateData
{
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? GroupId { get; set; }
    public string? ProviderType { get; set; }
    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasTools { get; set; }
    public bool IsStreaming { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseHeaders { get; set; }
    public bool ContentTruncated { get; set; }
    public string? OpenrouterKey { get; set; }
}

/// <summary>
/// 队列统计信息
/// </summary>
public class LogQueueStats
{
    /// <summary>
    /// 队列中待处理项目数量
    /// </summary>
    public int PendingCount { get; set; }
    
    /// <summary>
    /// 已处理项目总数
    /// </summary>
    public long ProcessedCount { get; set; }
    
    /// <summary>
    /// 处理失败项目总数
    /// </summary>
    public long FailedCount { get; set; }
    
    /// <summary>
    /// 丢弃项目总数（队列满时）
    /// </summary>
    public long DroppedCount { get; set; }
    
    /// <summary>
    /// 平均处理时间（毫秒）
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }
    
    /// <summary>
    /// 最后处理时间
    /// </summary>
    public DateTime? LastProcessedAt { get; set; }
    
    /// <summary>
    /// 队列是否健康
    /// </summary>
    public bool IsHealthy { get; set; }
    
    /// <summary>
    /// 健康状态描述
    /// </summary>
    public string HealthStatus { get; set; } = "正常";
}
