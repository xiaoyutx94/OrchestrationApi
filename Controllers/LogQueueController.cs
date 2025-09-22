using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Background;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 日志队列管理控制器
/// </summary>
[ApiController]
[Route("admin/log-queue")]
[Authorize]
public class LogQueueController : ControllerBase
{
    private readonly AsyncLogProcessingService _logProcessingService;
    private readonly ILogger<LogQueueController> _logger;

    public LogQueueController(
        AsyncLogProcessingService logProcessingService,
        ILogger<LogQueueController> logger)
    {
        _logProcessingService = logProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// 获取队列统计信息
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<ApiResponse<LogQueueStats>> GetQueueStats()
    {
        try
        {
            var stats = _logProcessingService.GetStats();
            return Ok(new ApiResponse<LogQueueStats>
            {
                Success = true,
                Data = stats,
                Message = "获取队列统计信息成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取队列统计信息时发生异常");
            return StatusCode(500, new ApiResponse<LogQueueStats>
            {
                Success = false,
                Message = "获取队列统计信息失败: " + ex.Message
            });
        }
    }

    /// <summary>
    /// 获取队列健康状态
    /// </summary>
    [HttpGet("health")]
    public ActionResult<ApiResponse<object>> GetQueueHealth()
    {
        try
        {
            var stats = _logProcessingService.GetStats();
            
            var healthInfo = new
            {
                IsHealthy = stats.IsHealthy,
                Status = stats.HealthStatus,
                PendingCount = stats.PendingCount,
                ProcessedCount = stats.ProcessedCount,
                FailedCount = stats.FailedCount,
                DroppedCount = stats.DroppedCount,
                LastProcessedAt = stats.LastProcessedAt,
                AverageProcessingTimeMs = stats.AverageProcessingTimeMs
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = healthInfo,
                Message = stats.IsHealthy ? "队列状态正常" : "队列状态异常"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取队列健康状态时发生异常");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "获取队列健康状态失败: " + ex.Message
            });
        }
    }
}
