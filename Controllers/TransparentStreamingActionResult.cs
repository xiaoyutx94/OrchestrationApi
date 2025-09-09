using Microsoft.AspNetCore.Mvc;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 透明HTTP代理流式响应结果
/// </summary>
public class TransparentStreamingActionResult : IActionResult
{
    private readonly Stream _responseStream;
    private readonly Dictionary<string, string> _headers;

    public TransparentStreamingActionResult(Stream responseStream, Dictionary<string, string> headers)
    {
        _responseStream = responseStream;
        _headers = headers;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        
        // 设置响应头
        foreach (var header in _headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                response.ContentType = header.Value;
            }
            else if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                // Transfer-Encoding 会自动处理，跳过
                continue;
            }
            else if (!response.Headers.ContainsKey(header.Key))
            {
                response.Headers[header.Key] = header.Value;
            }
        }

        // 确保流式响应的必要头部
        if (!response.Headers.ContainsKey("Cache-Control"))
            response.Headers["Cache-Control"] = "no-cache";
        if (!response.Headers.ContainsKey("Connection"))
            response.Headers["Connection"] = "keep-alive";

        try
        {
            // 直接将Provider的响应流透明地复制到客户端
            await _responseStream.CopyToAsync(response.Body);
        }
        catch (Exception ex)
        {
            // 如果流复制过程中发生异常，记录日志但不向客户端发送错误信息
            // 因为这可能会破坏正在进行的流式传输
            var logger = context.HttpContext.RequestServices
                .GetService<ILogger<TransparentStreamingActionResult>>();
            logger?.LogError(ex, "透明流式响应复制过程中发生异常");
        }
        finally
        {
            _responseStream?.Dispose();
        }
    }
}