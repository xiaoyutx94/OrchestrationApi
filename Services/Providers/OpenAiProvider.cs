using OrchestrationApi.Models;
using System.Text;
using Newtonsoft.Json;
using OrchestrationApi.Services.Core;
using System.Net;
using System.Diagnostics;

namespace OrchestrationApi.Services.Providers;

/// <summary>
/// OpenAI 服务商实现（纯 HttpClient 透明代理）
/// </summary>
public class OpenAiProvider : ILLMProvider
{
    private readonly IProxyHttpClientService _proxyHttpClientService;
    private readonly ILogger<OpenAiProvider> _logger;

    public string ProviderType => "openai";
    public bool SupportsStreaming => true;
    public bool SupportsTools => true;

    public OpenAiProvider(
        IProxyHttpClientService proxyHttpClientService,
        ILogger<OpenAiProvider> logger)
    {
        _proxyHttpClientService = proxyHttpClientService;
        _logger = logger;
    }

    /// <summary>
    /// 获取API基础URL
    /// </summary>
    /// <param name="config">提供商配置</param>
    /// <returns>API基础URL</returns>
    public string GetBaseUrl(ProviderConfig config)
    {
        return string.IsNullOrEmpty(config.BaseUrl)
            ? "https://api.openai.com"
            : config.BaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// 获取聊天完成端点
    /// </summary>
    /// <returns>聊天完成端点</returns>
    public string GetChatCompletionEndpoint()
    {
        return "/chat/completions";
    }

    /// <summary>
    /// 获取模型列表端点
    /// </summary>
    /// <returns>模型列
    /// 端点</returns>
    public string GetModelsEndpoint()
    {
        return "/models";
    }

    /// <summary>
    /// 获取Responses API端点
    /// </summary>
    /// <returns>Responses API端点</returns>
    public string GetResponsesEndpoint()
    {
        return "/responses";
    }

    /// <summary>
    /// 根据端点类型获取对应的端点路径
    /// </summary>
    /// <param name="endpointType">端点类型</param>
    /// <returns>端点路径</returns>
    private string GetEndpointByType(string endpointType)
    {
        return endpointType.ToLower() switch
        {
            "responses" => GetResponsesEndpoint(),
            "chat/completions" => GetChatCompletionEndpoint(),
            "models" => GetModelsEndpoint(),
            _ => GetChatCompletionEndpoint() // 默认使用chat/completions
        };
    }

    /// <summary>
    /// 准备HTTP请求内容
    /// </summary>
    /// <param name="request">请求</param>
    /// <param name="config">提供商配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP请求内容</returns>
    public Task<HttpContent> PrepareRequestContentAsync(
        ChatCompletionRequest request,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // 应用模型名称解析
        var resolvedRequest = new ChatCompletionRequest
        {
            Model = ResolveModelName(request.Model, config.ModelAliases),
            Messages = request.Messages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxTokens = request.MaxTokens,
            Stream = config.FakeStreaming ? false : request.Stream, // 假流模式下强制非流式
            PresencePenalty = request.PresencePenalty,
            FrequencyPenalty = request.FrequencyPenalty,
            Tools = request.Tools,
            Stop = request.Stop,
            StreamOptions = request.StreamOptions,
            LogitBias = request.LogitBias,
            User = request.User,

            // 新增参数透传
            N = request.N,
            FunctionCall = request.FunctionCall,
            Functions = request.Functions,
            ToolChoice = request.ToolChoice,
            ParallelToolCalls = request.ParallelToolCalls,
            ResponseFormat = request.ResponseFormat,
            Seed = request.Seed,
            ServiceTier = request.ServiceTier,

            // GPT-5/GPT-4o 新参数
            Verbosity = request.Verbosity,
            MinimalReasoning = request.MinimalReasoning,
            AllowedTools = request.AllowedTools,
            ReasoningEffort = request.ReasoningEffort,

            // 扩展参数
            Metadata = request.Metadata,
            Store = request.Store,

            // 新补充的重要参数
            TopLogprobs = request.TopLogprobs,
            Logprobs = request.Logprobs,
            MaxCompletionTokens = request.MaxCompletionTokens,
            Modalities = request.Modalities,
            Audio = request.Audio,
            Prediction = request.Prediction,
            DeveloperMessage = request.DeveloperMessage
        };

        // 应用参数覆盖
        foreach (var parameter in config.ParameterOverrides)
        {
            switch (parameter.Key.ToLower())
            {
                case "temperature":
                    if (float.TryParse(parameter.Value.ToString(), out var temp))
                        resolvedRequest.Temperature = temp;
                    break;

                case "top_p":
                    if (float.TryParse(parameter.Value.ToString(), out var topP))
                        resolvedRequest.TopP = topP;
                    break;

                case "max_tokens":
                    if (int.TryParse(parameter.Value.ToString(), out var maxTokens))
                        resolvedRequest.MaxTokens = maxTokens;
                    break;

                case "presence_penalty":
                    if (float.TryParse(parameter.Value.ToString(), out var presencePenalty))
                        resolvedRequest.PresencePenalty = presencePenalty;
                    break;

                case "frequency_penalty":
                    if (float.TryParse(parameter.Value.ToString(), out var frequencyPenalty))
                        resolvedRequest.FrequencyPenalty = frequencyPenalty;
                    break;

                case "n":
                    if (int.TryParse(parameter.Value.ToString(), out var n))
                        resolvedRequest.N = n;
                    break;

                case "seed":
                    if (int.TryParse(parameter.Value.ToString(), out var seed))
                        resolvedRequest.Seed = seed;
                    break;

                case "verbosity":
                    resolvedRequest.Verbosity = parameter.Value.ToString();
                    break;

                case "reasoning_effort":
                    resolvedRequest.ReasoningEffort = parameter.Value.ToString();
                    break;

                case "service_tier":
                    resolvedRequest.ServiceTier = parameter.Value.ToString();
                    break;

                case "minimal_reasoning":
                    if (bool.TryParse(parameter.Value.ToString(), out var minimalReasoning))
                        resolvedRequest.MinimalReasoning = minimalReasoning;
                    break;

                case "parallel_tool_calls":
                    if (bool.TryParse(parameter.Value.ToString(), out var parallelToolCalls))
                        resolvedRequest.ParallelToolCalls = parallelToolCalls;
                    break;

                case "store":
                    if (bool.TryParse(parameter.Value.ToString(), out var store))
                        resolvedRequest.Store = store;
                    break;

                case "top_logprobs":
                    if (int.TryParse(parameter.Value.ToString(), out var topLogprobs))
                        resolvedRequest.TopLogprobs = topLogprobs;
                    break;

                case "logprobs":
                    if (bool.TryParse(parameter.Value.ToString(), out var logprobs))
                        resolvedRequest.Logprobs = logprobs;
                    break;

                case "max_completion_tokens":
                    if (int.TryParse(parameter.Value.ToString(), out var maxCompletionTokens))
                        resolvedRequest.MaxCompletionTokens = maxCompletionTokens;
                    break;

                case "developer_message":
                    resolvedRequest.DeveloperMessage = parameter.Value.ToString();
                    break;
            }
        }

        var json = JsonConvert.SerializeObject(resolvedRequest,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

        return Task.FromResult<HttpContent>(new StringContent(json, Encoding.UTF8, "application/json"));
    }

    /// <summary>
    /// 从JSON字符串准备HTTP请求内容（用于透明代理模式）
    /// </summary>
    public Task<HttpContent> PrepareRequestContentFromJsonAsync(
        string requestJson,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // OpenAI使用标准JSON格式，直接透传
        // 注意：假流模式已在调用层处理，这里不需要特殊处理
        return Task.FromResult<HttpContent>(new StringContent(requestJson, Encoding.UTF8, "application/json"));
    }

    /// <summary>
    /// 准备HTTP请求头
    /// </summary>
    /// <param name="apiKey">API密钥</param>
    /// <param name="config">提供商配置</param>
    /// <returns>HTTP请求头</returns>
    public Dictionary<string, string> PrepareRequestHeaders(string apiKey, ProviderConfig config)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}",
            ["x-api-key"] = $"{apiKey}",
            ["Content-Type"] = "application/json"
        };

        // 添加自定义请求头
        foreach (var header in config.Headers)
        {
            headers[header.Key] = header.Value;
        }

        return headers;
    }

    /// <summary>
    /// 发送HTTP请求
    /// </summary>
    /// <param name="content">HTTP请求内容</param>
    /// <param name="apiKey">API密钥</param>
    /// <param name="config">提供商配置</param>
    /// <param name="isStreaming">是否流式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    public async Task<ProviderHttpResponse> SendHttpRequestAsync(
        HttpContent content,
        string apiKey,
        ProviderConfig config,
        bool isStreaming = false,
        CancellationToken cancellationToken = default)
    {
        if (config.ApiKeys.Count == 0)
        {
            return new ProviderHttpResponse
            {
                StatusCode = 400,
                IsSuccess = false,
                ErrorMessage = "至少需要一个API密钥",
                ShouldRetry = false,
                ShouldTryNextKey = false
            };
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return new ProviderHttpResponse
            {
                StatusCode = 400,
                IsSuccess = false,
                ErrorMessage = "API密钥不能为空",
                ShouldRetry = false,
                ShouldTryNextKey = false
            };
        }

        Exception? lastException = null;

        // 直接发送HTTP请求，不使用内部重试策略
        try
        {
            _logger.LogDebug("执行OpenAI HTTP请求, API密钥: {ApiKey}, 流式: {IsStreaming}, 分组: {GroupId}({GroupName})",
                MaskApiKey(apiKey), isStreaming, config.GroupId ?? "未知", config.GroupName ?? "未知");

            var baseUrl = GetBaseUrl(config);
            var endpoint = GetEndpointByType(config.EndpointType);
            var fullUrl = $"{baseUrl}{endpoint}";

            using var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Content = content;

            // 设置请求头
            var headers = PrepareRequestHeaders(apiKey, config);
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // 设置超时时间 - 区分连接超时和响应超时
            var connectionTimeoutSeconds = Math.Max(config.ConnectionTimeoutSeconds, 30);
            var responseTimeoutSeconds = config.ResponseTimeoutSeconds > 0 ? config.ResponseTimeoutSeconds : Math.Max(config.TimeoutSeconds, 30);
            using var responseTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(responseTimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, responseTimeoutCts.Token);

            _logger.LogDebug("OpenAI HTTP请求使用连接超时: {ConnectionTimeoutSeconds}秒, 响应超时: {ResponseTimeoutSeconds}秒, 分组: {GroupId}({GroupName})",
                connectionTimeoutSeconds, responseTimeoutSeconds, config.GroupId ?? "未知", config.GroupName ?? "未知");

            // 获取支持代理的HttpClient（使用连接超时设置）
            var proxyConfiguration = ConvertToProxyConfiguration(config.ProxyConfig);
            var httpClient = _proxyHttpClientService.CreateHttpClient(proxyConfiguration, connectionTimeoutSeconds);

            // 开始计时
            var stopwatch = Stopwatch.StartNew();

            // 判断是否需要假流：客户端要求流式 + 配置了假流模式
            var useFakeStreaming = isStreaming && config.FakeStreaming;
            var actualIsStreaming = useFakeStreaming ? false : isStreaming; // 假流模式下强制非流式请求上游

            // 发送请求
            var response = await httpClient.SendAsync(request,
                actualIsStreaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                combinedCts.Token);

            // 停止计时
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            var statusCode = (int)response.StatusCode;
            var responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            // 添加Content Headers
            if (response.Content.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("OpenAI HTTP请求成功，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, API密钥: {ApiKey}, 流式: {IsStreaming}, 假流: {UseFakeStreaming}, 分组: {GroupId}({GroupName})",
                    statusCode, elapsedMs, MaskApiKey(apiKey), isStreaming, useFakeStreaming, config.GroupId ?? "未知", config.GroupName ?? "未知");

                Stream responseStream;

                if (useFakeStreaming)
                {
                    // 假流模式：将非流式响应转换为流式格式
                    var nonStreamingContent = await response.Content.ReadAsStringAsync();
                    responseStream = ConvertToFakeStream(nonStreamingContent);

                    _logger.LogDebug("OpenAI 假流转换完成，原始响应长度: {ContentLength}, 分组: {GroupId}({GroupName})",
                        nonStreamingContent?.Length ?? 0, config.GroupId ?? "未知", config.GroupName ?? "未知");
                }
                else
                {
                    // 正常模式：直接返回响应流
                    responseStream = await response.Content.ReadAsStreamAsync();
                }

                return new ProviderHttpResponse
                {
                    StatusCode = statusCode,
                    IsSuccess = true,
                    ResponseStream = responseStream,
                    Headers = responseHeaders
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var (shouldRetry, shouldTryNextKey, errorMessage) = CheckErrorResponse(statusCode, errorContent);

                _logger.LogWarning("OpenAI HTTP请求失败，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, API密钥: {ApiKey}, 错误: {Error}, 分组: {GroupId}({GroupName})",
                    statusCode, elapsedMs, MaskApiKey(apiKey), errorMessage, config.GroupId ?? "未知", config.GroupName ?? "未知");

                return new ProviderHttpResponse
                {
                    StatusCode = statusCode,
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    Headers = responseHeaders,
                    ShouldRetry = shouldRetry,
                    ShouldTryNextKey = shouldTryNextKey
                };
            }
        }
        catch (Exception ex)
        {
            lastException = ex;
            _logger.LogError(ex, "OpenAI HTTP请求异常，API密钥: {ApiKey}, 分组: {GroupId}({GroupName})",
                MaskApiKey(apiKey), config.GroupId ?? "未知", config.GroupName ?? "未知");
        }

        _logger.LogError("无法完成OpenAI HTTP请求");

        return new ProviderHttpResponse
        {
            StatusCode = 500,
            IsSuccess = false,
            ErrorMessage = lastException?.Message ?? "请求失败",
            ShouldRetry = true,
            ShouldTryNextKey = true
        };
    }

    /// <summary>
    /// 检查响应是否需要重试或切换密钥
    /// </summary>
    /// <param name="statusCode">状态码</param>
    /// <param name="responseContent">响应内容</param>
    /// <returns>是否需要重试或切换密钥</returns>
    public (bool shouldRetry, bool shouldTryNextKey, string? errorMessage) CheckErrorResponse(
        int statusCode,
        string? responseContent)
    {
        var shouldRetry = false;
        var shouldTryNextKey = false;
        var errorMessage = $"HTTP {statusCode}";

        try
        {
            if (!string.IsNullOrEmpty(responseContent))
            {
                var errorResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var errorDetail = errorResponse?.error?.message?.ToString() ?? responseContent;
                errorMessage = $"HTTP {statusCode}: {errorDetail}";
            }
        }
        catch
        {
            // JSON 解析失败，使用原始内容
            if (!string.IsNullOrEmpty(responseContent))
                errorMessage = $"HTTP {statusCode}: {responseContent}";
        }

        switch (statusCode)
        {
            case 401: // Unauthorized - API key 无效
            case 403: // Forbidden - API key 权限不足
                shouldTryNextKey = true;
                break;

            case 429: // Rate Limited - 速率限制
                shouldRetry = true;
                shouldTryNextKey = true; // 也尝试下一个key
                break;

            case 500: // Internal Server Error
            case 502: // Bad Gateway
            case 503: // Service Unavailable
            case 504: // Gateway Timeout
                shouldRetry = true;
                break;

            case 400: // Bad Request - 通常是请求格式错误，不重试
            case 404: // Not Found
            case 422: // Unprocessable Entity
            default:
                // 其他错误不重试，也不切换key
                break;
        }

        return (shouldRetry, shouldTryNextKey, errorMessage);
    }

    /// <summary>
    /// 获取模型列表
    /// </summary>
    /// <param name="config">提供商配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表</returns>
    public async Task<ModelsResponse> GetModelsAsync(
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config.ApiKeys.Count == 0)
            throw new ArgumentException("至少需要一个API密钥", nameof(config));

        Exception? lastException = null;

        foreach (var apiKey in config.ApiKeys)
        {
            try
            {
                var baseUrl = GetBaseUrl(config);
                var endpoint = GetModelsEndpoint();
                var fullUrl = $"{baseUrl}{endpoint}";

                using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);

                // 设置请求头
                var headers = PrepareRequestHeaders(apiKey, config);
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                // 获取支持代理的HttpClient
                var proxyConfiguration = ConvertToProxyConfiguration(config.ProxyConfig);
                var httpClient = _proxyHttpClientService.CreateHttpClient(proxyConfiguration);

                var response = await httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(content);
                    return modelsResponse ?? new ModelsResponse { Data = new List<ModelInfo>() };
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var (_, shouldTryNextKey, _) = CheckErrorResponse((int)response.StatusCode, errorContent);

                if (shouldTryNextKey)
                    continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException($"获取模型列表超时 ({config.TimeoutSeconds}秒)");
                _logger.LogWarning("获取 OpenAI 模型列表超时");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "获取 OpenAI 模型列表失败");
            }
        }

        // 所有密钥尝试均失败时，不再返回默认模型，抛出异常让上层处理并提示失败
        var reason = lastException?.Message ?? "获取 OpenAI 模型列表失败";
        _logger.LogWarning("所有API密钥都无法获取模型列表，将返回错误而非默认模型: {Reason}", reason);
        throw lastException ?? new Exception("获取 OpenAI 模型列表失败");
    }

    public async Task<bool> ValidateApiKeyAsync(
        string apiKey,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testRequest = new ChatCompletionRequest
            {
                Model = "gpt-3.5-turbo",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "test" }
                },
                MaxTokens = 1
            };

            var content = await PrepareRequestContentAsync(testRequest, config, cancellationToken);
            var response = await SendHttpRequestAsync(content, apiKey, config, false, cancellationToken);

            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验证 OpenAI API 密钥失败");
            return false;
        }
    }

    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // 简单的Token估算，英文大约4个字符=1个token，中文大约1-2个字符=1个token
        var englishChars = System.Text.RegularExpressions.Regex.Matches(text, @"[a-zA-Z0-9\s\p{P}]").Count;
        var chineseChars = text.Length - englishChars;

        return (int)Math.Ceiling(englishChars / 4.0) + (int)Math.Ceiling(chineseChars / 1.5);
    }

    public string ResolveModelName(string requestedModel, Dictionary<string, string> aliases)
    {
        if (aliases.TryGetValue(requestedModel, out var aliasModel))
        {
            return aliasModel;
        }
        return requestedModel;
    }

    private string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
            return "***";

        return apiKey.Substring(0, 4) + "***" + apiKey.Substring(apiKey.Length - 4);
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

    /// <summary>
    /// 将非流式响应转换为假流式响应
    /// </summary>
    /// <param name="nonStreamingContent">非流式响应内容</param>
    /// <returns>假流式响应流</returns>
    private Stream ConvertToFakeStream(string nonStreamingContent)
    {
        try
        {
            // 解析非流式响应
            var response = JsonConvert.DeserializeObject<ChatCompletionResponse>(nonStreamingContent);
            if (response == null)
            {
                _logger.LogWarning("OpenAI 假流转换失败：无法解析响应内容");
                return new MemoryStream(Encoding.UTF8.GetBytes("data: [DONE]\n\n"));
            }

            var streamChunks = new List<string>();

            // 遍历每个choice，将其转换为流式chunk
            for (int choiceIndex = 0; choiceIndex < response.Choices.Count; choiceIndex++)
            {
                var choice = response.Choices[choiceIndex];

                // 如果有内容，分段发送
                var contentStr = choice.Message?.Content?.ToString();
                if (!string.IsNullOrEmpty(contentStr))
                {
                    const int chunkSize = 50; // 每个chunk的字符数

                    for (int i = 0; i < contentStr.Length; i += chunkSize)
                    {
                        var chunkContent = contentStr.Substring(i, Math.Min(chunkSize, contentStr.Length - i));

                        var streamChunk = new
                        {
                            id = response.Id,
                            @object = "chat.completion.chunk",
                            created = response.Created,
                            model = response.Model,
                            choices = new[]
                            {
                                new
                                {
                                    index = choiceIndex,
                                    delta = new { content = chunkContent },
                                    finish_reason = (string?)null
                                }
                            }
                        };

                        streamChunks.Add($"data: {JsonConvert.SerializeObject(streamChunk)}\n\n");
                    }
                }

                // 发送工具调用chunk（如果有）
                if (choice.Message?.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    foreach (var toolCall in choice.Message.ToolCalls)
                    {
                        var toolCallChunk = new
                        {
                            id = response.Id,
                            @object = "chat.completion.chunk",
                            created = response.Created,
                            model = response.Model,
                            choices = new[]
                            {
                                new
                                {
                                    index = choiceIndex,
                                    delta = new {
                                        tool_calls = new[]
                                        {
                                            new
                                            {
                                                index = 0,
                                                id = toolCall.Id,
                                                type = toolCall.Type,
                                                function = new
                                                {
                                                    name = toolCall.Function?.Name,
                                                    arguments = toolCall.Function?.Arguments
                                                }
                                            }
                                        }
                                    },
                                    finish_reason = (string?)null
                                }
                            }
                        };

                        streamChunks.Add($"data: {JsonConvert.SerializeObject(toolCallChunk)}\n\n");
                    }
                }

                // 发送结束chunk
                var finishChunk = new
                {
                    id = response.Id,
                    @object = "chat.completion.chunk",
                    created = response.Created,
                    model = response.Model,
                    choices = new[]
                    {
                        new
                        {
                            index = choiceIndex,
                            delta = new { },
                            finish_reason = choice.FinishReason ?? "stop"
                        }
                    }
                };

                streamChunks.Add($"data: {JsonConvert.SerializeObject(finishChunk)}\n\n");
            }

            // 添加结束标识
            streamChunks.Add("data: [DONE]\n\n");

            // 合并所有chunk
            var completeStreamContent = string.Join("", streamChunks);

            _logger.LogDebug("OpenAI 假流转换完成，生成了 {ChunkCount} 个chunk，总长度: {TotalLength}",
                streamChunks.Count, completeStreamContent.Length);

            return new MemoryStream(Encoding.UTF8.GetBytes(completeStreamContent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI 假流转换异常");
            // 返回基础的错误流
            return new MemoryStream(Encoding.UTF8.GetBytes("data: [DONE]\n\n"));
        }
    }
}