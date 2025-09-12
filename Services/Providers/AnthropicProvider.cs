using OrchestrationApi.Models;
using OrchestrationApi.Services.Core;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace OrchestrationApi.Services.Providers;

/// <summary>
/// Anthropic Claude 服务商实现（支持透明HTTP代理）
/// </summary>
public class AnthropicProvider : ILLMProvider
{
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly IProxyHttpClientService _proxyHttpClientService;

    public string ProviderType => "anthropic";
    public bool SupportsStreaming => true;
    public bool SupportsTools => true;

    public AnthropicProvider(ILogger<AnthropicProvider> logger, HttpClient httpClient, IProxyHttpClientService proxyHttpClientService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _proxyHttpClientService = proxyHttpClientService;
    }

    public string GetBaseUrl(ProviderConfig config)
    {
        return string.IsNullOrEmpty(config.BaseUrl) 
            ? "https://api.anthropic.com" 
            : config.BaseUrl.TrimEnd('/');
    }

    public string GetChatCompletionEndpoint()
    {
        return "/v1/messages";
    }

    public string GetModelsEndpoint()
    {
        return "/v1/models";
    }

    public Task<HttpContent> PrepareRequestContentAsync(
        ChatCompletionRequest request,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // 为了保持接口兼容性，这个方法抛出不支持异常
        // AnthropicProvider现在只支持原生格式
        throw new NotSupportedException("AnthropicProvider 不再支持 ChatCompletionRequest 格式，请使用 AnthropicMessageRequest 格式调用 PrepareAnthropicRequestContentAsync 方法");
    }

    public Task<HttpContent> PrepareAnthropicRequestContentAsync(
        AnthropicMessageRequest request,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // 直接使用Anthropic原生格式
        // 应用参数覆盖
        ApplyParameterOverrides(request, config.ParameterOverrides);
        
        var json = JsonConvert.SerializeObject(request, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        
        _logger.LogDebug("Anthropic原生API请求内容: {RequestContent}, 分组: {GroupId}({GroupName})", 
            json, config.GroupId ?? "未知", config.GroupName ?? "未知");
        
        return Task.FromResult<HttpContent>(new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public Dictionary<string, string> PrepareRequestHeaders(string apiKey, ProviderConfig config)
    {
        var headers = new Dictionary<string, string>
        {
            ["x-api-key"] = apiKey,
            ["anthropic-version"] = "2023-06-01",
            ["Content-Type"] = "application/json"
        };

        foreach (var header in config.Headers)
        {
            headers[header.Key] = header.Value;
        }

        return headers;
    }

    public async Task<ProviderHttpResponse> SendHttpRequestAsync(
        HttpContent content,
        string apiKey,
        ProviderConfig config,
        bool isStreaming = false,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl(config);
        var endpoint = GetChatCompletionEndpoint();
        var url = $"{baseUrl}{endpoint}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;

        // 设置请求头
        var headers = PrepareRequestHeaders(apiKey, config);
        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Content-Type已在HttpContent中设置
            }
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            // 设置超时时间 - 区分连接超时和响应超时，与OpenAiProvider保持一致
            var connectionTimeoutSeconds = Math.Max(config.ConnectionTimeoutSeconds, 30);
            var responseTimeoutSeconds = config.ResponseTimeoutSeconds > 0 ? config.ResponseTimeoutSeconds : Math.Max(config.TimeoutSeconds, 30);
            using var responseTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(responseTimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, responseTimeoutCts.Token);

            _logger.LogDebug("Anthropic HTTP请求使用连接超时: {ConnectionTimeoutSeconds}秒, 响应超时: {ResponseTimeoutSeconds}秒, 分组: {GroupId}({GroupName})", 
                connectionTimeoutSeconds, responseTimeoutSeconds, config.GroupId ?? "未知", config.GroupName ?? "未知");

            // 获取支持代理的HttpClient（使用连接超时设置）
            var proxyConfiguration = ConvertToProxyConfiguration(config.ProxyConfig);
            var httpClient = _proxyHttpClientService.CreateHttpClient(proxyConfiguration, connectionTimeoutSeconds);
            
            // 开始计时
            var stopwatch = Stopwatch.StartNew();
            
            var response = await httpClient.SendAsync(request, 
                isStreaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, 
                combinedCts.Token);

            // 停止计时
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            var responseHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Anthropic HTTP请求成功，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, 分组: {GroupId}({GroupName})",
                    (int)response.StatusCode, elapsedMs, config.GroupId ?? "未知", config.GroupName ?? "未知");

                var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return new ProviderHttpResponse
                {
                    IsSuccess = true,
                    StatusCode = (int)response.StatusCode,
                    Headers = responseHeaders,
                    ResponseStream = responseStream
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var (shouldRetry, shouldTryNextKey, errorMessage) = CheckErrorResponse((int)response.StatusCode, errorContent);
                
                _logger.LogWarning("Anthropic HTTP请求失败，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, 错误: {Error}, 分组: {GroupId}({GroupName})",
                    (int)response.StatusCode, elapsedMs, errorMessage, config.GroupId ?? "未知", config.GroupName ?? "未知");
                
                return new ProviderHttpResponse
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = errorMessage,
                    ShouldRetry = shouldRetry,
                    ShouldTryNextKey = shouldTryNextKey,
                    Headers = responseHeaders,
                    ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(errorContent))
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Anthropic API HTTP请求异常: {Message}, 分组: {GroupId}({GroupName})", 
                ex.Message, config.GroupId ?? "未知", config.GroupName ?? "未知");
            return new ProviderHttpResponse
            {
                IsSuccess = false,
                StatusCode = 500,
                ErrorMessage = $"HTTP请求异常: {ex.Message}",
                ShouldRetry = true,
                ShouldTryNextKey = false
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Anthropic API请求超时: {Message}, 分组: {GroupId}({GroupName})", 
                ex.Message, config.GroupId ?? "未知", config.GroupName ?? "未知");
            return new ProviderHttpResponse
            {
                IsSuccess = false,
                StatusCode = 408,
                ErrorMessage = "请求超时",
                ShouldRetry = true,
                ShouldTryNextKey = false
            };
        }
    }

    public (bool shouldRetry, bool shouldTryNextKey, string? errorMessage) CheckErrorResponse(
        int statusCode, 
        string? responseContent)
    {
        try
        {
            if (string.IsNullOrEmpty(responseContent))
            {
                return (false, false, $"HTTP {statusCode}");
            }

            var errorResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
            var errorType = errorResponse?.error?.type?.ToString();
            var errorMessage = errorResponse?.error?.message?.ToString() ?? $"HTTP {statusCode}";

            return statusCode switch
            {
                401 => (false, true, $"API密钥无效: {errorMessage}"), // 尝试下一个密钥
                403 => (false, true, $"API密钥权限不足: {errorMessage}"), // 尝试下一个密钥
                429 => (true, true, $"请求限制: {errorMessage}"), // 重试并尝试下一个密钥
                500 or 502 or 503 or 504 => (true, false, $"服务器错误: {errorMessage}"), // 重试同一密钥
                _ => (false, false, errorMessage)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析Anthropic错误响应失败，原始内容: {ResponseContent}", responseContent);
            return (false, false, $"HTTP {statusCode}");
        }
    }

    public Task<ModelsResponse> GetModelsAsync(
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!config.ApiKeys.Any())
            throw new ArgumentException("至少需要一个API密钥", nameof(config));

        // Anthropic 不提供模型列表API，直接返回预定义的模型列表
        _logger.LogInformation("返回Anthropic支持的模型列表, 分组: {GroupId}({GroupName})", 
            config.GroupId ?? "未知", config.GroupName ?? "未知");
        return Task.FromResult(GetSupportedModels());
    }

    public async Task<bool> ValidateApiKeyAsync(
        string apiKey,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 创建一个简单的测试请求
            var testRequest = new AnthropicMessageRequest
            {
                Model = "claude-3-haiku-20240307",
                Messages = new List<AnthropicMessage>
                {
                    new() 
                    { 
                        Role = "user", 
                        Content = new List<AnthropicContent>
                        {
                            new() { Type = "text", Text = "Hello" }
                        }
                    }
                },
                MaxTokens = 1
            };

            var content = await PrepareAnthropicRequestContentAsync(testRequest, config, cancellationToken);
            var response = await SendHttpRequestAsync(content, apiKey, config, false, cancellationToken);

            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验证Anthropic API密钥失败: {ApiKey}, 分组: {GroupId}({GroupName})", 
                apiKey.Substring(0, Math.Min(8, apiKey.Length)) + "...", config.GroupId ?? "未知", config.GroupName ?? "未知");
            return false;
        }
    }

    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Claude的Token计算 - 简化版本
        // 英文大约4个字符=1个token，中文大约2-3个字符=1个token
        var englishChars = Regex.Matches(text, @"[a-zA-Z0-9\s\p{P}]").Count;
        var chineseChars = text.Length - englishChars;

        return (int)Math.Ceiling(englishChars / 4.0) + (int)Math.Ceiling(chineseChars / 2.5);
    }

    public string ResolveModelName(string requestedModel, Dictionary<string, string> aliases)
    {
        if (aliases.TryGetValue(requestedModel, out var aliasModel))
        {
            return aliasModel;
        }

        // 映射常见的模型名称到标准模型ID
        var mappedModel = requestedModel.ToLower() switch
        {
            "claude-3-opus" or "claude-3-opus-20240229" => "claude-3-opus-20240229",
            "claude-3-haiku" or "claude-3-haiku-20240307" => "claude-3-haiku-20240307",
            "claude-3.5-sonnet" or "claude-3-5-sonnet" or "claude-3-5-sonnet-20241022" => "claude-3-5-sonnet-20241022",
            "claude-3.5-haiku" or "claude-3-5-haiku" or "claude-3-5-haiku-20241022" => "claude-3-5-haiku-20241022",
            _ => requestedModel
        };

        return mappedModel;
    }


    /// <summary>
    /// 应用参数覆盖
    /// </summary>
    private void ApplyParameterOverrides(AnthropicMessageRequest request, Dictionary<string, object> overrides)
    {
        foreach (var kvp in overrides)
        {
            switch (kvp.Key.ToLower())
            {
                case "max_tokens":
                    if (kvp.Value is int maxTokens)
                        request.MaxTokens = maxTokens;
                    break;
                case "temperature":
                    if (kvp.Value is float temperature)
                        request.Temperature = temperature;
                    break;
                case "top_p":
                    if (kvp.Value is float topP)
                        request.TopP = topP;
                    break;
                case "top_k":
                    if (kvp.Value is int topK)
                        request.TopK = topK;
                    break;
            }
        }
    }

    private static ModelsResponse GetSupportedModels()
    {
        return new ModelsResponse
        {
            Data = new List<ModelInfo>
            {
                new() { Id = "claude-3-5-sonnet-20241022", OwnedBy = "anthropic", Created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new() { Id = "claude-3-5-haiku-20241022", OwnedBy = "anthropic", Created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new() { Id = "claude-3-opus-20240229", OwnedBy = "anthropic", Created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new() { Id = "claude-3-sonnet-20240229", OwnedBy = "anthropic", Created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new() { Id = "claude-3-haiku-20240307", OwnedBy = "anthropic", Created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            }
        };
    }

    /// <summary>
    /// 将ProxyConfig转换为ProxyConfiguration
    /// </summary>
    /// <param name="proxyConfig">Provider的代理配置</param>
    /// <returns>ProxyConfiguration实例</returns>
    private ProxyConfiguration? ConvertToProxyConfiguration(ProxyConfig? proxyConfig)
    {
        if (proxyConfig == null || string.IsNullOrEmpty(proxyConfig.Host))
        {
            return null;
        }

        return new ProxyConfiguration
        {
            Type = proxyConfig.Type,
            Host = proxyConfig.Host,
            Port = proxyConfig.Port,
            Username = proxyConfig.Username ?? "",
            Password = proxyConfig.Password ?? "",
            BypassLocal = proxyConfig.BypassLocal,
            BypassDomains = proxyConfig.BypassDomains ?? new List<string>()
        };
    }
}