using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using OrchestrationApi.Services.Providers;
using System.Text;

namespace OrchestrationApi.Controllers;

/// <summary>
/// OpenAI 兼容 API v1 控制器（透明HTTP代理模式）
/// </summary>
[ApiController]
[Route("v1")]
[Produces("application/json")]
public class V1Controller : ControllerBase
{
    private readonly IMultiProviderService _multiProviderService;
    private readonly IProviderFactory _providerFactory;
    private readonly ILogger<V1Controller> _logger;
    private readonly string _providerType = "openai";

    public V1Controller(
        IMultiProviderService multiProviderService,
        IProviderFactory providerFactory,
        ILogger<V1Controller> logger)
    {
        _multiProviderService = multiProviderService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// 聊天完成（透明HTTP代理模式）
    /// </summary>
    /// <returns>HTTP响应</returns>
    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions()
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
            ChatCompletionRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<ChatCompletionRequest>(rawJsonBody) 
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
            var proxyKey = httpRequest.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
            if(string.IsNullOrEmpty(proxyKey))
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

            _logger.LogDebug("接收到聊天完成请求 - Model: {Model}, Stream: {Stream}, ProxyKey: {ProxyKey}，原始请求：{RawRequest}",
                request.Model, request.Stream, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", rawJsonBody);

            // 使用透明HTTP代理（内部会进行模型验证和路由）
            var httpResponse = await _multiProviderService.ProcessChatCompletionHttpAsync(
                request, proxyKey, _providerType, clientIp, userAgent, httpRequest.Path, HttpContext.RequestAborted);

            if (!httpResponse.IsSuccess)
            {
                _logger.LogWarning("Provider HTTP请求失败 - StatusCode: {StatusCode}, Error: {Error}", 
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

            if (request.Stream && httpResponse.ResponseStream != null)
            {
                // 透明流式响应
                _logger.LogDebug("返回透明流式响应");
                return new TransparentStreamingActionResult(httpResponse.ResponseStream, httpResponse.Headers);
            }
            else if (httpResponse.ResponseStream != null)
            {
                // 非流式响应，读取完整内容
                _logger.LogDebug("返回非流式响应");
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
                _logger.LogError("Provider返回的响应流为null");
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
            _logger.LogError(ex, "处理聊天完成请求时发生异常");
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
    /// 获取模型列表
    /// </summary>
    /// <returns>模型列表</returns>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try
        {
            var proxyKey = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if(string.IsNullOrEmpty(proxyKey))
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
            var response = await _multiProviderService.GetAvailableModelsAsync(proxyKey, _providerType);
            return Ok(response);
        }
        catch (Exception ex)
        {
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
