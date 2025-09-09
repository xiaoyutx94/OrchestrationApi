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
}