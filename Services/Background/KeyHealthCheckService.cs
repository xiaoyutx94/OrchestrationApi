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
/// 密钥健康检查后台服务
/// 定期检查所有启用分组中无效key是否已经恢复正常
/// </summary>
public class KeyHealthCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KeyHealthCheckService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;

    public KeyHealthCheckService(
        IServiceProvider serviceProvider,
        ILogger<KeyHealthCheckService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // 从配置文件读取检查间隔，默认为5分钟
        var intervalMinutes = _configuration.GetValue<int>("OrchestrationApi:KeyHealthCheck:IntervalMinutes", 5);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 检查服务是否启用
        var isEnabled = _configuration.GetValue<bool>("OrchestrationApi:KeyHealthCheck:Enabled", true);
        if (!isEnabled)
        {
            _logger.LogInformation("密钥健康检查服务已禁用，服务不会启动");
            return;
        }

        _logger.LogInformation("密钥健康检查服务已启动，检查间隔: {Interval} 分钟", _checkInterval.TotalMinutes);

        // 等待应用程序完全启动
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行密钥健康检查时发生异常");
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

        _logger.LogInformation("密钥健康检查服务已停止");
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        try
        {
            _logger.LogDebug("开始执行密钥健康检查...");

            // 使用KeyManager中的统一方法检查和恢复密钥
            var result = await keyManager.CheckAndRecoverInvalidKeysAsync();
            
            // 记录结果
            if (result is Dictionary<string, object> resultDict)
            {
                var success = resultDict.GetValueOrDefault("success", false);
                var recoveredKeys = resultDict.GetValueOrDefault("recovered_keys", 0);
                var checkedGroups = resultDict.GetValueOrDefault("checked_groups", 0);
                var message = resultDict.GetValueOrDefault("message", "未知结果");

                if (success is true)
                {
                    if (recoveredKeys is int recovered && recovered > 0)
                    {
                        _logger.LogInformation("密钥健康检查完成 - 检查了 {CheckedGroups} 个分组，恢复了 {RecoveredKeys} 个密钥",
                            checkedGroups, recovered);
                    }
                    else
                    {
                        _logger.LogDebug("密钥健康检查完成 - 检查了 {CheckedGroups} 个分组，没有发现需要恢复的密钥",
                            checkedGroups);
                    }
                }
                else
                {
                    var error = resultDict.GetValueOrDefault("error", "未知错误");
                    _logger.LogError("密钥健康检查失败: {Error}", error);
                }
            }
            else
            {
                _logger.LogWarning("密钥健康检查返回了意外的结果格式");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密钥健康检查过程中发生异常");
        }
    }

}
