using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using OrchestrationApi.Services.Providers;
using System.Text;

namespace OrchestrationApi.Controllers;

/// <summary>
/// Anthropic 原生 API 控制器
/// </summary>
[ApiController]
[Route("claude/v1")]
[Produces("application/json")]
public class AnthropicController : ControllerBase
{
    private readonly IMultiProviderService _multiProviderService;
    private readonly ILogger<AnthropicController> _logger;
    private readonly string _providerType = "anthropic";

    public AnthropicController(
        IMultiProviderService multiProviderService,
        IProviderFactory providerFactory,
        ILogger<AnthropicController> logger)
    {
        _multiProviderService = multiProviderService;
        _logger = logger;
    }

    /// <summary>
    /// Anthropic 原生消息API（支持流式和非流式）
    /// </summary>
    /// <returns>Anthropic 原生格式响应</returns>
    [HttpPost("messages")]
    public async Task<IActionResult> Messages()
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
            AnthropicMessageRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<AnthropicMessageRequest>(rawJsonBody)
                    ?? throw new ArgumentException("Invalid JSON format");
            }
            catch (JsonException ex)
            {
                return BadRequest(new
                {
                    error = new
                    {
                        type = "invalid_request_error",
                        message = $"Invalid JSON format: {ex.Message}"
                    }
                });
            }

            var httpRequest = HttpContext.Request;
            // 从请求头获取 x-api-key 用作 ProxyKey 判断
            var proxyKey = httpRequest.Headers["x-api-key"].FirstOrDefault();
            if (string.IsNullOrEmpty(proxyKey))
            {
                return BadRequest(new
                {
                    error = new
                    {
                        type = "authentication_error",
                        message = "ProxyKey is required"
                    }
                });
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpRequest.Headers.UserAgent.FirstOrDefault();

            _logger.LogDebug("接收到Anthropic原生消息请求 - Model: {Model}, Stream: {Stream}, ProxyKey: {ProxyKey}，原始请求：{RawRequest}",
                request.Model, request.Stream, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", rawJsonBody);

            // 使用Anthropic原生HTTP代理
            var httpResponse = await _multiProviderService.ProcessAnthropicRequestAsync(
                request, proxyKey, clientIp, userAgent, httpRequest.Path, HttpContext.RequestAborted);

            if (!httpResponse.IsSuccess)
            {
                _logger.LogWarning("Anthropic API请求失败 - StatusCode: {StatusCode}, Error: {Error}",
                    httpResponse.StatusCode, httpResponse.ErrorMessage);

                return StatusCode(httpResponse.StatusCode, new
                {
                    error = new
                    {
                        type = "provider_error",
                        message = httpResponse.ErrorMessage ?? "请求失败"
                    }
                });
            }

            if (request.Stream && httpResponse.ResponseStream != null)
            {
                // 透明流式响应
                _logger.LogDebug("返回Anthropic原生流式响应");
                return new TransparentStreamingActionResult(httpResponse.ResponseStream, httpResponse.Headers);
            }
            else if (httpResponse.ResponseStream != null)
            {
                // 非流式响应，读取完整内容
                _logger.LogDebug("返回Anthropic原生非流式响应");
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
                _logger.LogError("Anthropic API返回的响应流为null");
                return StatusCode(500, new
                {
                    error = new
                    {
                        type = "internal_error",
                        message = "内部处理错误"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理Anthropic原生消息请求时发生异常");
            return BadRequest(new
            {
                error = new
                {
                    type = "invalid_request_error",
                    message = ex.Message
                }
            });
        }
    }

    /// <summary>
    /// 获取Anthropic支持的模型列表
    /// </summary>
    /// <returns>模型列表</returns>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try
        {
            // 与 /v1/messages 一致，从 x-api-key 读取代理密钥
            var proxyKey = HttpContext.Request.Headers["x-api-key"].FirstOrDefault();
            if (string.IsNullOrEmpty(proxyKey))
            {
                return BadRequest(new
                {
                    error = new
                    {
                        type = "authentication_error",
                        message = "ProxyKey is required"
                    }
                });
            }

            var response = await _multiProviderService.GetAvailableModelsAsync(proxyKey, _providerType);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = new
                {
                    type = "invalid_request_error",
                    message = ex.Message
                }
            });
        }
    }
}