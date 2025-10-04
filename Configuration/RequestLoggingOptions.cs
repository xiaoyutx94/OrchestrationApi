namespace OrchestrationApi.Configuration;

/// <summary>
/// 请求日志配置选项
/// </summary>
public class RequestLoggingOptions
{
    /// <summary>
    /// 是否启用请求日志
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否启用记录详细内容（请求体和响应体）
    /// </summary>
    public bool EnableDetailedContent { get; set; } = false;

    /// <summary>
    /// 最大内容长度（超过则截断）
    /// </summary>
    public int MaxContentLength { get; set; } = 10000;

    /// <summary>
    /// 是否排除健康检查请求
    /// </summary>
    public bool ExcludeHealthChecks { get; set; } = true;

    /// <summary>
    /// 日志保留天数（自动清理）
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// 队列配置
    /// </summary>
    public LogQueueOptions Queue { get; set; } = new();
}

/// <summary>
/// 日志队列配置选项
/// </summary>
public class LogQueueOptions
{
    /// <summary>
    /// 是否启用异步队列处理（false则使用同步模式）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 队列最大容量
    /// </summary>
    public int MaxCapacity { get; set; } = 10000;

    /// <summary>
    /// 批处理大小
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 处理间隔（毫秒）
    /// </summary>
    public int ProcessingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// 队列满时的处理策略
    /// </summary>
    public QueueFullStrategy FullStrategy { get; set; } = QueueFullStrategy.DropOldest;

    /// <summary>
    /// 优雅关闭超时时间（毫秒）
    /// </summary>
    public int GracefulShutdownTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// 队列满时的处理策略
/// </summary>
public enum QueueFullStrategy
{
    /// <summary>
    /// 丢弃最旧的日志
    /// </summary>
    DropOldest,

    /// <summary>
    /// 拒绝新的日志
    /// </summary>
    RejectNew,

    /// <summary>
    /// 阻塞等待（不推荐，可能影响性能）
    /// </summary>
    Block
}