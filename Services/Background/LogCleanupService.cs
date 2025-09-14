using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchestrationApi.Services.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrchestrationApi.Services.Background;

/// <summary>
/// 日志清理后台服务
/// 定期清理过期的请求日志记录
/// </summary>
public class LogCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogCleanupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _cleanupInterval;

    public LogCleanupService(
        IServiceProvider serviceProvider,
        ILogger<LogCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // 从配置文件读取清理间隔，默认为24小时（每天清理一次）
        var intervalHours = _configuration.GetValue<int>("OrchestrationApi:LogCleanup:IntervalHours", 24);
        _cleanupInterval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 检查服务是否启用
        var isEnabled = _configuration.GetValue<bool>("OrchestrationApi:LogCleanup:Enabled", true);
        if (!isEnabled)
        {
            _logger.LogInformation("日志清理后台服务已禁用，服务不会启动");
            return;
        }

        _logger.LogInformation("日志清理后台服务已启动，清理间隔: {Interval} 小时", _cleanupInterval.TotalHours);

        // 等待应用程序完全启动
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        // 启动时立即执行一次清理（如果配置允许）
        var cleanupOnStartup = _configuration.GetValue<bool>("OrchestrationApi:LogCleanup:CleanupOnStartup", false);
        if (cleanupOnStartup)
        {
            try
            {
                await PerformLogCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动时执行日志清理失败");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformLogCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行定期日志清理时发生异常");
            }

            // 等待下次清理
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
        }

        _logger.LogInformation("日志清理后台服务已停止");
    }

    /// <summary>
    /// 执行日志清理
    /// </summary>
    private async Task PerformLogCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var requestLogger = scope.ServiceProvider.GetRequiredService<IRequestLogger>();

        try
        {
            _logger.LogDebug("开始执行定期日志清理...");

            // 调用RequestLogger的清理方法
            await requestLogger.CleanupOldLogsAsync();
            
            _logger.LogInformation("定期日志清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定期日志清理过程中发生异常");
        }
    }
}