using OrchestrationApi.Models;

namespace OrchestrationApi.Services.Providers;

/// <summary>
/// LLM服务商配置
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// API密钥列表
    /// </summary>
    public List<string> ApiKeys { get; set; } = new();

    /// <summary>
    /// 基础URL
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 超时时间（秒）- 向后兼容，等同于ResponseTimeoutSeconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 响应超时时间（秒）- 完整响应的时间限制
    /// </summary>
    public int ResponseTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 自定义请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 模型别名映射
    /// </summary>
    public Dictionary<string, string> ModelAliases { get; set; } = new();

    /// <summary>
    /// 参数覆盖
    /// </summary>
    public Dictionary<string, object> ParameterOverrides { get; set; } = new();

    /// <summary>
    /// 模型
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 服务商分组ID（用于日志记录和追踪）
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 服务商分组名称（用于日志记录）
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 代理配置
    /// </summary>
    public ProxyConfig? ProxyConfig { get; set; }

    /// <summary>
    /// 假流模式：将非流式响应伪装成流式响应输出给客户端
    /// 主要用于不支持流式输出的上游API
    /// </summary>
    public bool FakeStreaming { get; set; } = false;

    /// <summary>
    /// API端点类型，用于区分不同的API端点
    /// </summary>
    public string EndpointType { get; set; } = "chat/completions";
}

/// <summary>
/// 代理配置
/// </summary>
public class ProxyConfig
{
    /// <summary>
    /// 代理类型 (http, https, socks5)
    /// </summary>
    public string Type { get; set; } = "http";

    /// <summary>
    /// 代理主机地址
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 代理端口
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 认证用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 认证密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 是否绕过本地地址
    /// </summary>
    public bool BypassLocal { get; set; } = true;

    /// <summary>
    /// 绕过代理的域名列表
    /// </summary>
    public List<string> BypassDomains { get; set; } = new();
}

/// <summary>
/// Provider HTTP 响应结果
/// </summary>
public class ProviderHttpResponse
{
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public Stream? ResponseStream { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool ShouldRetry { get; set; }
    public bool ShouldTryNextKey { get; set; }
}

/// <summary>
/// LLM服务商接口
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// 服务商类型
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// 是否支持流式响应
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// 是否支持工具调用
    /// </summary>
    bool SupportsTools { get; }

    /// <summary>
    /// API 基础 URL
    /// </summary>
    string GetBaseUrl(ProviderConfig config);

    /// <summary>
    /// 获取聊天完成端点
    /// </summary>
    string GetChatCompletionEndpoint();

    /// <summary>
    /// 获取模型列表端点
    /// </summary>
    string GetModelsEndpoint();

    /// <summary>
    /// 从JSON字符串准备HTTP请求内容（用于透明代理模式）
    /// </summary>
    Task<HttpContent> PrepareRequestContentFromJsonAsync(
        string requestJson,
        ProviderConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 准备HTTP请求头
    /// </summary>
    Dictionary<string, string> PrepareRequestHeaders(
        string apiKey,
        ProviderConfig config);

    /// <summary>
    /// 透明HTTP请求（支持流式和非流式）
    /// </summary>
    Task<ProviderHttpResponse> SendHttpRequestAsync(
        HttpContent content,
        string apiKey,
        ProviderConfig config,
        bool isStreaming = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查响应是否需要重试或切换密钥
    /// </summary>
    (bool shouldRetry, bool shouldTryNextKey, string? errorMessage) CheckErrorResponse(
        int statusCode,
        string? responseContent);

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    Task<ModelsResponse> GetModelsAsync(
        ProviderConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 估算Token数量
    /// </summary>
    int EstimateTokens(string text);

    /// <summary>
    /// 转换模型名称（处理别名映射）
    /// </summary>
    string ResolveModelName(string requestedModel, Dictionary<string, string> aliases);
}