using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using OrchestrationApi.Services.Providers;
using System.Text;

namespace OrchestrationApi.Controllers;

/// <summary>
/// Gemini 兼容 API 控制器（透明HTTP代理模式）
/// </summary>
[ApiController]
[Route("v1beta")]
[Produces("application/json")]
public class GeminiController : ControllerBase
{
    private readonly IMultiProviderService _multiProviderService;
    private readonly IProviderFactory _providerFactory;
    private readonly ILogger<GeminiController> _logger;
    private readonly string _providerType = "gemini";

    public GeminiController(
        IMultiProviderService multiProviderService,
        IProviderFactory providerFactory,
        ILogger<GeminiController> logger)
    {
        _multiProviderService = multiProviderService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gemini 生成内容（非流式）
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <returns>HTTP响应</returns>
    [HttpPost("models/{model}:generateContent")]
    public async Task<IActionResult> GenerateContent(string model)
    {
        try
        {
            // 读取原始JSON请求体
            string rawJsonBody;
            using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                rawJsonBody = await reader.ReadToEndAsync();
            }

            // 手动反序列化请求对象
            GeminiGenerateContentRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<GeminiGenerateContentRequest>(rawJsonBody)
                    ?? throw new ArgumentException("Invalid JSON format");
            }
            catch (JsonException ex)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = $"Invalid JSON format: {ex.Message}",
                        Type = "invalid_request_error"
                    }
                });
            }

            var httpRequest = HttpContext.Request;
            var proxyKey = HttpContext.Request.Headers["x-goog-api-key"].FirstOrDefault();
            if (string.IsNullOrEmpty(proxyKey))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "ProxyKey is required",
                        Type = "invalid_request_error"
                    }
                });
            }
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpRequest.Headers.UserAgent.FirstOrDefault();

            _logger.LogDebug("接收到Gemini生成内容请求 - Model: {Model}, ProxyKey: {ProxyKey}，原始请求：{RawRequest}",
                model, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", rawJsonBody);

            // 使用Gemini专用HTTP代理
            var httpResponse = await _multiProviderService.ProcessGeminiHttpRequestAsync(
                request, false, proxyKey, _providerType, clientIp, userAgent,
                httpRequest.Path, HttpContext.RequestAborted);

            if (!httpResponse.IsSuccess)
            {
                _logger.LogWarning("Gemini Provider HTTP请求失败 - StatusCode: {StatusCode}, Error: {Error}",
                    httpResponse.StatusCode, httpResponse.ErrorMessage);

                return StatusCode(httpResponse.StatusCode, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = httpResponse.ErrorMessage ?? "请求失败",
                        Type = "provider_error"
                    }
                });
            }

            if (httpResponse.ResponseStream != null)
            {
                // 非流式响应，读取完整内容
                _logger.LogDebug("返回Gemini非流式响应");
                using var reader = new StreamReader(httpResponse.ResponseStream);
                var content = await reader.ReadToEndAsync();

                // 设置响应头
                foreach (var header in httpResponse.Headers)
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        Response.ContentType = header.Value;
                    }
                    else if (!Response.Headers.ContainsKey(header.Key))
                    {
                        Response.Headers[header.Key] = header.Value;
                    }
                }

                return Content(content, Response.ContentType ?? "application/json");
            }
            else
            {
                _logger.LogError("Gemini Provider返回的响应流为null");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "内部处理错误",
                        Type = "internal_error"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理Gemini生成内容请求时发生异常");
            return BadRequest(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = ex.Message,
                    Type = "invalid_request_error"
                }
            });
        }
    }

    /// <summary>
    /// Gemini 流式生成内容
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <returns>HTTP响应</returns>
    [HttpPost("models/{model}:streamGenerateContent")]
    public async Task<IActionResult> StreamGenerateContent(string model)
    {
        try
        {
            // 读取原始JSON请求体
            string rawJsonBody;
            using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                rawJsonBody = await reader.ReadToEndAsync();
            }

            // 手动反序列化请求对象
            GeminiGenerateContentRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<GeminiGenerateContentRequest>(rawJsonBody)
                    ?? throw new ArgumentException("Invalid JSON format");
            }
            catch (JsonException ex)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = $"Invalid JSON format: {ex.Message}",
                        Type = "invalid_request_error"
                    }
                });
            }

            var httpRequest = HttpContext.Request;
            var proxyKey = HttpContext.Request.Headers["x-goog-api-key"].FirstOrDefault();
            if (string.IsNullOrEmpty(proxyKey))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "ProxyKey is required",
                        Type = "invalid_request_error"
                    }
                });
            }
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpRequest.Headers.UserAgent.FirstOrDefault();

            _logger.LogDebug("接收到Gemini流式生成内容请求 - Model: {Model}, ProxyKey: {ProxyKey}，原始请求：{RawRequest}",
                model, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", rawJsonBody);

            request.Model = model;

            // 使用Gemini专用HTTP代理，启用流式
            var httpResponse = await _multiProviderService.ProcessGeminiHttpRequestAsync(
                request, true, proxyKey, _providerType, clientIp, userAgent,
                httpRequest.Path, HttpContext.RequestAborted);

            if (!httpResponse.IsSuccess)
            {
                _logger.LogWarning("Gemini Provider流式HTTP请求失败 - StatusCode: {StatusCode}, Error: {Error}",
                    httpResponse.StatusCode, httpResponse.ErrorMessage);

                return StatusCode(httpResponse.StatusCode, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = httpResponse.ErrorMessage ?? "请求失败",
                        Type = "provider_error"
                    }
                });
            }

            if (httpResponse.ResponseStream != null)
            {
                // 透明流式响应
                _logger.LogDebug("返回Gemini透明流式响应");
                return new TransparentStreamingActionResult(httpResponse.ResponseStream, httpResponse.Headers);
            }
            else
            {
                _logger.LogError("Gemini Provider返回的流式响应流为null");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "内部处理错误",
                        Type = "internal_error"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理Gemini流式生成内容请求时发生异常");
            return BadRequest(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = ex.Message,
                    Type = "invalid_request_error"
                }
            });
        }
    }

    /// <summary>
    /// 获取Gemini模型列表（使用新的过滤逻辑，去除models/前缀）
    /// </summary>
    /// <param name="pageToken">分页令牌（暂时保留兼容性，实际未使用）</param>
    /// <returns>过滤后的模型列表</returns>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string? pageToken = null)
    {
        try
        {
            var proxyKey = HttpContext.Request.Headers["x-goog-api-key"].FirstOrDefault();
            if (string.IsNullOrEmpty(proxyKey))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "ProxyKey is required",
                        Type = "invalid_request_error"
                    }
                });
            }

            _logger.LogDebug("获取Gemini模型列表 - ProxyKey: {ProxyKey}", string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

            // 使用新的 GetGeminiAvailableModelsAsync 方法，自动去除 models/ 前缀
            var response = await _multiProviderService.GetGeminiAvailableModelsAsync(proxyKey, _providerType);

            _logger.LogDebug("返回 {ModelCount} 个Gemini模型", response.Models?.Count ?? 0);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Gemini模型列表时发生异常");
            return BadRequest(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = ex.Message,
                    Type = "invalid_request_error"
                }
            });
        }
    }
}