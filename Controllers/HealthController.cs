using Microsoft.AspNetCore.Mvc;
using OrchestrationApi.Services.Core;
using SqlSugar;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 健康检查控制器
/// </summary>
[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IVersionService _versionService;
    private readonly IHealthCheckService _healthCheckService;
    private static readonly DateTime _startTime = DateTime.Now;

    public HealthController(
        ISqlSugarClient db,
        ILogger<HealthController> logger,
        IConfiguration configuration,
        IVersionService versionService,
        IHealthCheckService healthCheckService)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
        _versionService = versionService;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// 基础健康检查
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult Health()
    {
        try
        {
            var uptime = DateTime.Now - _startTime;
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.Now,
                uptime = uptime.ToString(@"dd\.hh\:mm\:ss"),
                version = _versionService.GetCurrentVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康检查失败");
            return StatusCode(500, new
            {
                status = "unhealthy",
                timestamp = DateTime.Now,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 详细健康检查
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> DetailedHealth()
    {
        try
        {
            var uptime = DateTime.Now - _startTime;
            var healthChecks = new Dictionary<string, object>();

            // 数据库连接检查
            try
            {
                await _db.Queryable<Models.GroupConfig>().Take(1).ToListAsync();
                healthChecks["database"] = new { status = "healthy", message = "数据库连接正常" };
            }
            catch (Exception ex)
            {
                healthChecks["database"] = new { status = "unhealthy", message = ex.Message };
            }



            // 磁盘空间检查
            var driveInfo = new DriveInfo(Directory.GetCurrentDirectory());
            var freeSpaceGB = driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024;
            healthChecks["disk"] = new 
            { 
                status = freeSpaceGB > 1 ? "healthy" : "warning", 
                free_space_gb = freeSpaceGB,
                message = $"可用磁盘空间: {freeSpaceGB}GB"
            };

            var overallStatus = healthChecks.Values.All(v => 
                v.GetType().GetProperty("status")?.GetValue(v)?.ToString() == "healthy") ? "healthy" : "degraded";

            return Ok(new
            {
                status = overallStatus,
                timestamp = DateTime.Now,
                uptime = uptime.ToString(@"dd\.hh\:mm\:ss"),
                version = _versionService.GetCurrentVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                checks = healthChecks
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "详细健康检查失败");
            return StatusCode(500, new
            {
                status = "unhealthy",
                timestamp = DateTime.Now,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 就绪检查 (Kubernetes readiness probe)
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Ready()
    {
        try
        {
            // 检查数据库连接
            await _db.Queryable<Models.GroupConfig>().Take(1).ToListAsync();
            
            return Ok(new { status = "ready" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "就绪检查失败");
            return StatusCode(503, new { status = "not_ready", error = ex.Message });
        }
    }

    /// <summary>
    /// 存活检查 (Kubernetes liveness probe)
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(200)]
    public IActionResult Live()
    {
        return Ok(new { status = "alive" });
    }

    /// <summary>
    /// 检查版本更新
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(typeof(VersionCheckResult), 200)]
    public async Task<IActionResult> CheckVersion()
    {
        try
        {
            var result = await _versionService.CheckForUpdatesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "版本检查失败");
            return StatusCode(500, new
            {
                error = "版本检查失败",
                message = ex.Message,
                current_version = _versionService.GetCurrentVersion()
            });
        }
    }

    /// <summary>
    /// 测试健康检查分析功能 (仅用于验证修复)
    /// </summary>
    [HttpGet("test-analysis/{groupId}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> TestHealthCheckAnalysis(string groupId)
    {
        try
        {
            // 获取最近的健康检查结果
            var recentResults = await _healthCheckService.GetRecentHealthCheckResultsAsync(groupId, null, 50);

            if (!recentResults.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = "暂无健康检查数据",
                    group_id = groupId
                });
            }

            // 分析健康检查一致性
            var analysis = _healthCheckService.AnalyzeHealthCheckConsistency(recentResults);

            var result = new
            {
                success = true,
                group_id = groupId,
                analysis_time = DateTime.Now,

                // 一致性分析结果
                consistency_analysis = new
                {
                    provider_healthy = analysis.ProviderHealthy,
                    keys_healthy = analysis.KeysHealthy,
                    models_healthy = analysis.ModelsHealthy,
                    is_inconsistent = analysis.IsInconsistent,
                    inconsistency_reason = analysis.InconsistencyReason,
                    overall_healthy = analysis.IsOverallHealthy,
                    success_rate = Math.Round(analysis.SuccessRate, 2),
                    status_summary = analysis.GetStatusSummary(),
                    detailed_explanation = analysis.GetDetailedExplanation()
                },

                // 统计信息
                statistics = new
                {
                    total_checks = analysis.TotalChecks,
                    successful_checks = analysis.SuccessfulChecks,
                    failed_checks = analysis.FailedChecks,
                    data_period = new
                    {
                        from = recentResults.Min(r => r.CheckedAt),
                        to = recentResults.Max(r => r.CheckedAt)
                    }
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试健康检查分析时发生异常: {GroupId}", groupId);
            return StatusCode(500, new { success = false, error = "分析失败", details = ex.Message });
        }
    }
}
