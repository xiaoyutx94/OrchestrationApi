using Newtonsoft.Json;

namespace OrchestrationApi.Utils;

/// <summary>
/// 密钥验证 / 模型健康检查用的最小探测请求构造。
/// </summary>
public static class ProbeRequestBuilder
{
    /// <summary>
    /// 判断是否为常见 reasoning / o 系列模型（对 temperature、max_tokens 敏感）。
    /// </summary>
    public static bool IsReasoningLikeModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        var m = modelId.Trim();
        if (m.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return m.Contains("reason", StringComparison.OrdinalIgnoreCase)
               || m.Contains("thinking", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// OpenAI 兼容 chat/completions 探测 JSON。
    /// </summary>
    public static string BuildOpenAiChatProbeJson(string modelId, string userText = "Hello")
    {
        var requestDict = new Dictionary<string, object>
        {
            ["model"] = modelId,
            ["messages"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["role"] = "user",
                    ["content"] = userText
                }
            },
            ["stream"] = false
        };

        if (IsReasoningLikeModel(modelId))
        {
            // o1/o3 等通常不接受 temperature，且倾向 max_completion_tokens
            requestDict["max_completion_tokens"] = 16;
        }
        else
        {
            requestDict["max_tokens"] = 16;
            requestDict["temperature"] = 0;
        }

        return JsonConvert.SerializeObject(requestDict, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <summary>
    /// Anthropic messages 探测 JSON。
    /// </summary>
    public static string BuildAnthropicMessagesProbeJson(string modelId, string userText = "Hello")
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = modelId,
            ["max_tokens"] = 16,
            ["stream"] = false,
            ["messages"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["role"] = "user",
                    ["content"] = new List<Dictionary<string, object>>
                    {
                        new()
                        {
                            ["type"] = "text",
                            ["text"] = userText
                        }
                    }
                }
            }
        };

        return JsonConvert.SerializeObject(request, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <summary>
    /// Gemini generateContent 探测 JSON。
    /// </summary>
    public static string BuildGeminiGenerateContentProbeJson(string userText = "Hi")
    {
        var request = new Dictionary<string, object>
        {
            ["contents"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["role"] = "user",
                    ["parts"] = new List<Dictionary<string, object>>
                    {
                        new() { ["text"] = userText }
                    }
                }
            },
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["maxOutputTokens"] = 1,
                ["temperature"] = 0
            }
        };

        return JsonConvert.SerializeObject(request, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <summary>
    /// 截断上游错误正文，便于落库/前端展示。
    /// </summary>
    public static string TruncateUpstreamBody(string? body, int maxLen = 800)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var trimmed = body.Trim().Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (trimmed.Length <= maxLen) return trimmed;
        return trimmed[..maxLen] + "…";
    }

    /// <summary>
    /// 将 HTTP 状态码映射为用户可读摘要（模型探测）。
    /// </summary>
    public static string MapModelProbeErrorSummary(int statusCode)
    {
        return statusCode switch
        {
            400 => "探测请求参数/格式不被上游接受（不一定表示密钥失效或模型完全不可用）",
            401 => "API密钥在模型端点无效",
            403 => "权限不足或额度/策略限制（不一定是没额度）",
            404 => "模型不存在或端点不可用",
            429 => "模型请求限流",
            500 => "模型服务内部错误",
            502 => "上游网关错误",
            503 => "模型服务不可用",
            _ => $"模型检查失败 (HTTP {statusCode})"
        };
    }

    /// <summary>
    /// 组合摘要 + 上游原文。
    /// </summary>
    public static string ComposeErrorMessage(int statusCode, string? upstreamBody)
    {
        var summary = MapModelProbeErrorSummary(statusCode);
        var body = TruncateUpstreamBody(upstreamBody);
        return string.IsNullOrEmpty(body) ? summary : $"{summary} | upstream: {body}";
    }
}
