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
    private static readonly DateTime _startTime = DateTime.Now;

    public HealthController(
        ISqlSugarClient db, 
        ILogger<HealthController> logger, 
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
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
                version = "1.0.0",
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


            // 内存使用检查
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            healthChecks["memory"] = new 
            { 
                status = memoryUsageMB < 1024 ? "healthy" : "warning", 
                usage_mb = memoryUsageMB,
                message = $"内存使用: {memoryUsageMB}MB"
            };

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
                version = "1.0.0",
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
}
