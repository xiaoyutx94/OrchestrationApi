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
    private readonly ILogger<V1Controller> _logger;

    public V1Controller(
        IMultiProviderService multiProviderService,
        IProviderFactory providerFactory,
        ILogger<V1Controller> logger)
    {
        _multiProviderService = multiProviderService;
        _logger = logger;
    }

    /// <summary>
    /// 根据请求路径确定provider类型
    /// </summary>
    /// <param name="requestPath">请求路径</param>
    /// <returns>provider类型</returns>
    private string GetProviderTypeFromPath(string requestPath)
    {
        if (requestPath.Contains("/responses"))
        {
            return "openai_responses";
        }
        return "openai";
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

            // 验证JSON格式（不反序列化为实体类）
            try
            {
                JsonConvert.DeserializeObject<object>(rawJsonBody);
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

            // 根据请求路径动态确定provider类型
            var providerType = GetProviderTypeFromPath(httpRequest.Path);

            _logger.LogDebug("接收到聊天完成请求 - ProxyKey: {ProxyKey}, ProviderType: {ProviderType}，原始请求：{RawRequest}",
                string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", providerType, rawJsonBody);

            // 使用透明HTTP代理（直接传递JSON字符串，内部会进行模型验证和路由）
            var httpResponse = await _multiProviderService.ProcessChatCompletionHttpAsync(
                rawJsonBody, proxyKey, providerType, clientIp, userAgent, httpRequest.Path, HttpContext.RequestAborted);

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

            // 判断是否为流式请求（从JSON中提取）
            var isStreamRequest = false;
            try
            {
                var requestDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawJsonBody);
                if (requestDict?.ContainsKey("stream") == true)
                {
                    isStreamRequest = Convert.ToBoolean(requestDict["stream"]);
                }
            }
            catch
            {
                // 忽略解析错误，默认非流式
            }

            if (isStreamRequest && httpResponse.ResponseStream != null)
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

            // 根据请求路径动态确定provider类型
            var providerType = GetProviderTypeFromPath(HttpContext.Request.Path);

            var response = await _multiProviderService.GetAvailableModelsAsync(proxyKey, providerType);
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

    /// <summary>
    /// Responses API端点（OpenAI兼容）
    /// </summary>
    /// <returns>HTTP响应</returns>
    [HttpPost("responses")]
    public async Task<IActionResult> Responses()
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
            ResponsesRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<ResponsesRequest>(rawJsonBody)
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

            // 根据请求路径动态确定provider类型
            var providerType = GetProviderTypeFromPath(httpRequest.Path);

            _logger.LogDebug("接收到Responses请求 - Model: {Model}, Stream: {Stream}, ProxyKey: {ProxyKey}, ProviderType: {ProviderType}，原始请求：{RawRequest}",
                request.Model, request.Stream, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供", providerType, rawJsonBody);

            // 使用透明HTTP代理处理Responses请求
            var httpResponse = await _multiProviderService.ProcessResponsesHttpAsync(
                request, proxyKey, providerType, clientIp, userAgent, httpRequest.Path, HttpContext.RequestAborted);

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
            _logger.LogError(ex, "处理Responses请求时发生异常");
            return StatusCode(500, new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = "内部服务器错误",
                    Type = "internal_error"
                }
            });
        }
    }

    /// <summary>
    /// 检索之前的响应（支持链式调用）
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <returns>响应详情</returns>
    [HttpGet("responses/{responseId}")]
    public async Task<IActionResult> GetResponse(string responseId)
    {
        try
        {
            var httpRequest = HttpContext.Request;
            var proxyKey = httpRequest.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
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

            var result = await _multiProviderService.RetrieveResponseAsync(responseId, proxyKey);
            if (result == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "Response not found",
                        Type = "not_found_error"
                    }
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索响应时发生异常: {ResponseId}", responseId);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = "内部服务器错误",
                    Type = "internal_error"
                }
            });
        }
    }

    /// <summary>
    /// 删除存储的响应
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <returns>删除确认</returns>
    [HttpDelete("responses/{responseId}")]
    public async Task<IActionResult> DeleteResponse(string responseId)
    {
        try
        {
            var httpRequest = HttpContext.Request;
            var proxyKey = httpRequest.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
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

            var success = await _multiProviderService.DeleteResponseAsync(responseId, proxyKey);
            if (!success)
            {
                return NotFound(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "Response not found",
                        Type = "not_found_error"
                    }
                });
            }

            return Ok(new { deleted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除响应时发生异常: {ResponseId}", responseId);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = "内部服务器错误",
                    Type = "internal_error"
                }
            });
        }
    }

    /// <summary>
    /// 取消后台响应任务
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <returns>取消确认</returns>
    [HttpPost("responses/{responseId}/cancel")]
    public async Task<IActionResult> CancelResponse(string responseId)
    {
        try
        {
            var httpRequest = HttpContext.Request;
            var proxyKey = httpRequest.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
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

            var result = await _multiProviderService.CancelResponseAsync(responseId, proxyKey);
            if (result == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Message = "Response not found",
                        Type = "not_found_error"
                    }
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消响应时发生异常: {ResponseId}", responseId);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Message = "内部服务器错误",
                    Type = "internal_error"
                }
            });
        }
    }
}