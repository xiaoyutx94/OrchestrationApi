using Microsoft.Extensions.Options;
using OrchestrationApi.Configuration;
using OrchestrationApi.Models;
using SqlSugar;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrchestrationApi.Services.Background;

/// <summary>
/// 异步日志处理后台服务
/// </summary>
public class AsyncLogProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AsyncLogProcessingService> _logger;
    private readonly RequestLoggingOptions _options;
    private readonly ConcurrentQueue<LogQueueItem> _logQueue;
    private readonly SemaphoreSlim _processingSignal;
    private readonly LogQueueStats _stats;
    private readonly object _statsLock = new();
    private readonly List<double> _processingTimes = new();

    public AsyncLogProcessingService(
        IServiceProvider serviceProvider,
        ILogger<AsyncLogProcessingService> logger,
        IOptions<RequestLoggingOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _logQueue = new ConcurrentQueue<LogQueueItem>();
        _processingSignal = new SemaphoreSlim(0);
        _stats = new LogQueueStats { IsHealthy = true };
    }

    /// <summary>
    /// 获取队列统计信息
    /// </summary>
    public LogQueueStats GetStats()
    {
        lock (_statsLock)
        {
            _stats.PendingCount = _logQueue.Count;
            _stats.AverageProcessingTimeMs = _processingTimes.Count > 0 
                ? _processingTimes.Average() 
                : 0;
            return _stats;
        }
    }

    /// <summary>
    /// 将日志项加入队列
    /// </summary>
    public bool EnqueueLog(LogQueueItem item)
    {
        if (!_options.Enabled || !_options.Queue.Enabled)
        {
            return false;
        }

        // 检查队列容量
        if (_logQueue.Count >= _options.Queue.MaxCapacity)
        {
            return HandleQueueFull(item);
        }

        _logQueue.Enqueue(item);
        _processingSignal.Release(); // 通知处理线程
        return true;
    }

    /// <summary>
    /// 处理队列满的情况
    /// </summary>
    private bool HandleQueueFull(LogQueueItem newItem)
    {
        switch (_options.Queue.FullStrategy)
        {
            case QueueFullStrategy.DropOldest:
                // 丢弃最旧的项目，添加新项目
                if (_logQueue.TryDequeue(out _))
                {
                    _logQueue.Enqueue(newItem);
                    lock (_statsLock)
                    {
                        _stats.DroppedCount++;
                    }
                    _logger.LogWarning("队列已满，丢弃最旧的日志项");
                    return true;
                }
                return false;

            case QueueFullStrategy.RejectNew:
                // 拒绝新项目
                lock (_statsLock)
                {
                    _stats.DroppedCount++;
                }
                _logger.LogWarning("队列已满，拒绝新的日志项");
                return false;

            case QueueFullStrategy.Block:
                // 阻塞等待（不推荐）
                _logQueue.Enqueue(newItem);
                _logger.LogWarning("队列已满，但仍然添加了新项目（可能影响性能）");
                return true;

            default:
                return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.Queue.Enabled)
        {
            _logger.LogInformation("异步日志处理服务已禁用");
            return;
        }

        _logger.LogInformation("异步日志处理服务已启动，批处理大小: {BatchSize}, 处理间隔: {IntervalMs}ms", 
            _options.Queue.BatchSize, _options.Queue.ProcessingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待信号或超时
                await _processingSignal.WaitAsync(
                    TimeSpan.FromMilliseconds(_options.Queue.ProcessingIntervalMs), 
                    stoppingToken);

                if (_logQueue.IsEmpty)
                {
                    continue;
                }

                await ProcessLogBatch(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常关闭
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理日志队列时发生异常");
                
                // 更新健康状态
                lock (_statsLock)
                {
                    _stats.IsHealthy = false;
                    _stats.HealthStatus = $"处理异常: {ex.Message}";
                }

                // 等待一段时间后重试
                await Task.Delay(TimeSpan.FromMilliseconds(_options.Queue.RetryDelayMs), stoppingToken);
            }
        }

        // 优雅关闭：处理剩余的日志项
        await ProcessRemainingLogs(stoppingToken);
    }

    /// <summary>
    /// 处理一批日志项
    /// </summary>
    private async Task ProcessLogBatch(CancellationToken cancellationToken)
    {
        var batch = new List<LogQueueItem>();
        var stopwatch = Stopwatch.StartNew();

        // 收集一批日志项
        for (int i = 0; i < _options.Queue.BatchSize && _logQueue.TryDequeue(out var item); i++)
        {
            batch.Add(item);
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

            await ProcessBatchItems(db, batch, cancellationToken);

            stopwatch.Stop();
            
            // 更新统计信息
            lock (_statsLock)
            {
                _stats.ProcessedCount += batch.Count;
                _stats.LastProcessedAt = DateTime.Now;
                _stats.IsHealthy = true;
                _stats.HealthStatus = "正常";
                
                _processingTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                if (_processingTimes.Count > 100) // 只保留最近100次的处理时间
                {
                    _processingTimes.RemoveAt(0);
                }
            }

            _logger.LogDebug("成功处理 {Count} 个日志项，耗时 {ElapsedMs}ms", 
                batch.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批处理日志项时发生异常，批次大小: {BatchSize}", batch.Count);
            
            // 处理失败的项目，根据重试策略决定是否重新入队
            await HandleFailedBatch(batch, ex);
        }
    }

    /// <summary>
    /// 处理批次中的日志项
    /// </summary>
    private async Task ProcessBatchItems(ISqlSugarClient db, List<LogQueueItem> batch, CancellationToken cancellationToken)
    {
        var insertItems = batch.Where(x => x.Type == LogQueueItemType.Insert && x.LogData != null).ToList();
        var updateItems = batch.Where(x => x.Type == LogQueueItemType.Update && x.UpdateData != null).ToList();

        // 批量插入
        if (insertItems.Count > 0)
        {
            var logsToInsert = insertItems.Select(x => x.LogData!).ToList();
            await db.Insertable(logsToInsert).ExecuteCommandAsync();
        }

        // 批量更新
        foreach (var updateItem in updateItems)
        {
            var updateData = updateItem.UpdateData!;
            await db.Updateable<RequestLog>()
                .SetColumns(it => new RequestLog
                {
                    StatusCode = updateData.StatusCode,
                    DurationMs = updateData.DurationMs,
                    GroupId = updateData.GroupId,
                    ProviderType = updateData.ProviderType,
                    Model = updateData.Model,
                    PromptTokens = updateData.PromptTokens,
                    CompletionTokens = updateData.CompletionTokens,
                    TotalTokens = updateData.TotalTokens,
                    ErrorMessage = updateData.ErrorMessage,
                    HasTools = updateData.HasTools,
                    IsStreaming = updateData.IsStreaming,
                    ResponseBody = updateData.ResponseBody,
                    ResponseHeaders = updateData.ResponseHeaders,
                    ContentTruncated = updateData.ContentTruncated,
                    OpenrouterKey = updateData.OpenrouterKey
                })
                .Where(rl => rl.RequestId == updateItem.RequestId)
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 处理失败的批次
    /// </summary>
    private async Task HandleFailedBatch(List<LogQueueItem> batch, Exception exception)
    {
        lock (_statsLock)
        {
            _stats.FailedCount += batch.Count;
        }

        foreach (var item in batch)
        {
            item.RetryCount++;
            item.LastError = exception.Message;

            if (item.RetryCount <= _options.Queue.MaxRetries)
            {
                // 重新入队
                _logQueue.Enqueue(item);
                _logger.LogWarning("日志项 {RequestId} 处理失败，将重试 ({RetryCount}/{MaxRetries})", 
                    item.RequestId, item.RetryCount, _options.Queue.MaxRetries);
            }
            else
            {
                _logger.LogError("日志项 {RequestId} 达到最大重试次数，放弃处理。错误: {Error}", 
                    item.RequestId, exception.Message);
            }
        }

        // 等待一段时间后重试
        await Task.Delay(TimeSpan.FromMilliseconds(_options.Queue.RetryDelayMs));
    }

    /// <summary>
    /// 处理剩余的日志项（优雅关闭时）
    /// </summary>
    private async Task ProcessRemainingLogs(CancellationToken cancellationToken)
    {
        if (_logQueue.IsEmpty)
        {
            return;
        }

        _logger.LogInformation("正在处理剩余的 {Count} 个日志项...", _logQueue.Count);

        var timeout = TimeSpan.FromMilliseconds(_options.Queue.GracefulShutdownTimeoutMs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (!_logQueue.IsEmpty && !timeoutCts.Token.IsCancellationRequested)
            {
                await ProcessLogBatch(timeoutCts.Token);
            }

            if (_logQueue.IsEmpty)
            {
                _logger.LogInformation("所有剩余日志项已处理完成");
            }
            else
            {
                _logger.LogWarning("优雅关闭超时，仍有 {Count} 个日志项未处理", _logQueue.Count);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("优雅关闭被取消，仍有 {Count} 个日志项未处理", _logQueue.Count);
        }
    }
}
