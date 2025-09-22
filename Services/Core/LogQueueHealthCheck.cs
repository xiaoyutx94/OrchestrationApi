using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchestrationApi.Services.Background;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 日志队列健康检查
/// </summary>
public class LogQueueHealthCheck : IHealthCheck
{
    private readonly AsyncLogProcessingService _logProcessingService;
    private readonly ILogger<LogQueueHealthCheck> _logger;

    public LogQueueHealthCheck(
        AsyncLogProcessingService logProcessingService,
        ILogger<LogQueueHealthCheck> logger)
    {
        _logProcessingService = logProcessingService;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = _logProcessingService.GetStats();
            
            // 检查队列是否健康
            if (!stats.IsHealthy)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"日志队列状态异常: {stats.HealthStatus}",
                    data: new Dictionary<string, object>
                    {
                        ["pending_count"] = stats.PendingCount,
                        ["processed_count"] = stats.ProcessedCount,
                        ["failed_count"] = stats.FailedCount,
                        ["dropped_count"] = stats.DroppedCount,
                        ["last_processed_at"] = stats.LastProcessedAt?.ToString() ?? "从未处理",
                        ["average_processing_time_ms"] = stats.AverageProcessingTimeMs
                    }));
            }

            // 检查队列是否积压过多
            if (stats.PendingCount > 5000) // 可配置的阈值
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"日志队列积压过多: {stats.PendingCount} 个待处理项目",
                    data: new Dictionary<string, object>
                    {
                        ["pending_count"] = stats.PendingCount,
                        ["processed_count"] = stats.ProcessedCount,
                        ["failed_count"] = stats.FailedCount,
                        ["dropped_count"] = stats.DroppedCount,
                        ["last_processed_at"] = stats.LastProcessedAt?.ToString() ?? "从未处理",
                        ["average_processing_time_ms"] = stats.AverageProcessingTimeMs
                    }));
            }

            // 检查是否有过多的失败或丢弃
            var totalProcessed = stats.ProcessedCount + stats.FailedCount + stats.DroppedCount;
            if (totalProcessed > 0)
            {
                var failureRate = (double)(stats.FailedCount + stats.DroppedCount) / totalProcessed;
                if (failureRate > 0.1) // 失败率超过10%
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"日志队列失败率过高: {failureRate:P2}",
                        data: new Dictionary<string, object>
                        {
                            ["pending_count"] = stats.PendingCount,
                            ["processed_count"] = stats.ProcessedCount,
                            ["failed_count"] = stats.FailedCount,
                            ["dropped_count"] = stats.DroppedCount,
                            ["failure_rate"] = failureRate,
                            ["last_processed_at"] = stats.LastProcessedAt?.ToString() ?? "从未处理",
                            ["average_processing_time_ms"] = stats.AverageProcessingTimeMs
                        }));
                }
            }

            // 队列状态正常
            return Task.FromResult(HealthCheckResult.Healthy(
                "日志队列状态正常",
                data: new Dictionary<string, object>
                {
                    ["pending_count"] = stats.PendingCount,
                    ["processed_count"] = stats.ProcessedCount,
                    ["failed_count"] = stats.FailedCount,
                    ["dropped_count"] = stats.DroppedCount,
                    ["last_processed_at"] = stats.LastProcessedAt?.ToString() ?? "从未处理",
                    ["average_processing_time_ms"] = stats.AverageProcessingTimeMs
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查日志队列健康状态时发生异常");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"无法检查日志队列状态: {ex.Message}"));
        }
    }
}
