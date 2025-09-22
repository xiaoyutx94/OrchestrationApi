using OrchestrationApi.Services.Core;

namespace OrchestrationApi.Services.Background;

/// <summary>
/// 健康检查后台服务
/// 定期执行健康检查任务，支持通过配置文件设置检查频率
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;
    private readonly bool _isEnabled;

    public HealthCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HealthCheckBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // 从配置文件读取检查间隔，默认为30分钟
        var intervalMinutes = _configuration.GetValue<int>("OrchestrationApi:HealthCheck:IntervalMinutes", 30);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
        
        // 从配置文件读取是否启用，默认启用
        _isEnabled = _configuration.GetValue<bool>("OrchestrationApi:HealthCheck:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_isEnabled)
        {
            _logger.LogInformation("健康检查后台服务已禁用，服务不会启动");
            return;
        }

        _logger.LogInformation("健康检查后台服务已启动，检查间隔: {Interval} 分钟", _checkInterval.TotalMinutes);

        // 等待应用程序完全启动
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        // 启动时立即执行一次健康检查（如果配置允许）
        var checkOnStartup = _configuration.GetValue<bool>("OrchestrationApi:HealthCheck:CheckOnStartup", true);
        if (checkOnStartup)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动时执行健康检查失败");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行定期健康检查时发生异常");
            }

            // 等待下次检查
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
        }

        _logger.LogInformation("健康检查后台服务已停止");
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();

        try
        {
            _logger.LogDebug("开始执行健康检查...");
            var startTime = DateTime.Now;

            // 执行所有分组的健康检查
            var results = await healthCheckService.CheckAllGroupsHealthAsync(cancellationToken);
            
            if (results.Count > 0)
            {
                // 批量保存健康检查结果
                await healthCheckService.SaveHealthCheckResultsAsync(results, cancellationToken);
                
                var duration = DateTime.Now - startTime;
                var successCount = results.Count(r => r.IsSuccess);
                var failureCount = results.Count - successCount;
                
                _logger.LogInformation("健康检查完成 - 总数: {Total}, 成功: {Success}, 失败: {Failure}, 耗时: {Duration}ms", 
                    results.Count, successCount, failureCount, (int)duration.TotalMilliseconds);

                // 记录失败的检查详情
                var failures = results.Where(r => !r.IsSuccess).ToList();
                if (failures.Count > 0)
                {
                    _logger.LogWarning("发现 {Count} 个健康检查失败:", failures.Count);
                    foreach (var failure in failures.Take(10)) // 只记录前10个失败
                    {
                        _logger.LogWarning("- {GroupId} ({CheckType}): {StatusCode} - {ErrorMessage}", 
                            failure.GroupId, failure.CheckType, failure.StatusCode, failure.ErrorMessage);
                    }
                    
                    if (failures.Count > 10)
                    {
                        _logger.LogWarning("... 还有 {Count} 个失败未显示", failures.Count - 10);
                    }
                }
            }
            else
            {
                _logger.LogInformation("没有找到需要检查的分组");
            }

            // 清理过期记录（每次检查时都执行，但只在配置允许时）
            var enableCleanup = _configuration.GetValue<bool>("OrchestrationApi:HealthCheck:EnableCleanup", true);
            if (enableCleanup)
            {
                var retentionDays = _configuration.GetValue<int>("OrchestrationApi:HealthCheck:RetentionDays", 30);
                var cleanedCount = await healthCheckService.CleanupExpiredHealthCheckRecordsAsync(retentionDays, cancellationToken);
                
                if (cleanedCount > 0)
                {
                    _logger.LogInformation("清理了 {Count} 条过期的健康检查记录", cleanedCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行健康检查时发生异常");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止健康检查后台服务...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("健康检查后台服务已停止");
    }
}

/// <summary>
/// 健康检查配置选项
/// </summary>
public class HealthCheckOptions
{
    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 检查间隔（分钟）
    /// </summary>
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>
    /// 启动时是否立即检查
    /// </summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>
    /// 是否启用清理过期记录
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// 记录保留天数
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// 并发检查的最大分组数
    /// </summary>
    public int MaxConcurrentGroups { get; set; } = 5;

    /// <summary>
    /// 单个检查的超时时间（秒）
    /// </summary>
    public int CheckTimeoutSeconds { get; set; } = 30;
}
