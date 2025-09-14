using OrchestrationApi.Models;
using System.Text;
using Newtonsoft.Json;
using OrchestrationApi.Services.Core;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace OrchestrationApi.Services.Providers;

/// <summary>
/// Google Gemini 服务商实现（纯 HttpClient 透明代理）
/// </summary>
public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiProvider> _logger;
    private readonly IConfiguration _configuration;

    public string ProviderType => "gemini";
    public bool SupportsStreaming => true;
    public bool SupportsTools => true;

    public GeminiProvider(
        HttpClient httpClient,
        ILogger<GeminiProvider> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public string GetBaseUrl(ProviderConfig config)
    {
        return string.IsNullOrEmpty(config.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : config.BaseUrl.TrimEnd('/');
    }

    public string ChatCompletionEndpoint => "/v1beta/models/{model}:generateContent"; // 非流式端点

    public string StreamingEndpoint => "/v1beta/models/{model}:streamGenerateContent?alt=sse"; // 流式端点

    public string ModelsEndpoint => "/v1beta/models";

    // 接口方法实现（调用属性）
    public string GetChatCompletionEndpoint() => ChatCompletionEndpoint;

    public string GetModelsEndpoint() => ModelsEndpoint;

    public Task<HttpContent> PrepareRequestContentAsync(
        ChatCompletionRequest request,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // 将OpenAI格式转换为Gemini格式
        var geminiRequest = ConvertToGeminiRequest(request, config);

        var jsonContent = JsonConvert.SerializeObject(geminiRequest, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return Task.FromResult<HttpContent>(content);
    }

    public Task<HttpContent> PrepareRequestContentAsync(
        GeminiGenerateContentRequest request,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        // 对于Gemini原生请求，直接序列化
        var jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return Task.FromResult<HttpContent>(content);
    }

    public Dictionary<string, string> PrepareRequestHeaders(string apiKey, ProviderConfig config)
    {
        var headers = new Dictionary<string, string>
        {
            ["x-goog-api-key"] = apiKey,
            ["Content-Type"] = "application/json"
        };

        // 添加自定义请求头
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

        try
        {
            _logger.LogDebug("执行Gemini HTTP请求, API密钥: {ApiKey}, 流式: {IsStreaming}, 分组: {GroupId}({GroupName})",
                MaskApiKey(apiKey), isStreaming, config.GroupId ?? "未知", config.GroupName ?? "未知");

            var baseUrl = GetBaseUrl(config);
            var endpoint = isStreaming ? StreamingEndpoint : ChatCompletionEndpoint;

            // Gemini需要在URL中包含模型参数
            var model = config.Model;
            endpoint = endpoint.Replace("{model}", model);

            var fullUrl = $"{baseUrl}{endpoint}";

            using var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Content = content;

            // 设置请求头
            var headers = PrepareRequestHeaders(apiKey, config);
            foreach (var header in headers)
            {
                if (header.Key == "Content-Type")
                    continue; // Content-Type 已经通过 HttpContent 设置

                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // 设置超时时间 - 从配置中读取Gemini专用超时设置，区分连接超时和响应超时
            var connectionTimeoutSeconds = Math.Max(_configuration.GetValue<int>("OrchestrationApi:Gemini:ConnectionTimeout", 30), config.ConnectionTimeoutSeconds);
            var responseTimeoutSeconds = isStreaming 
                ? Math.Max(_configuration.GetValue<int>("OrchestrationApi:Gemini:StreamingTimeout", 300), config.ResponseTimeoutSeconds > 0 ? config.ResponseTimeoutSeconds : config.TimeoutSeconds)
                : Math.Max(_configuration.GetValue<int>("OrchestrationApi:Gemini:NonStreamingTimeout", 180), config.ResponseTimeoutSeconds > 0 ? config.ResponseTimeoutSeconds : config.TimeoutSeconds);
            using var responseTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(responseTimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, responseTimeoutCts.Token);

            _logger.LogDebug("Gemini HTTP请求使用连接超时: {ConnectionTimeoutSeconds}秒, 响应超时: {ResponseTimeoutSeconds}秒, 流式: {IsStreaming}, 分组: {GroupId}({GroupName})", 
                connectionTimeoutSeconds, responseTimeoutSeconds, isStreaming, config.GroupId ?? "未知", config.GroupName ?? "未知");

            // 开始计时
            var stopwatch = Stopwatch.StartNew();

            // 发送请求
            var response = await _httpClient.SendAsync(request,
                isStreaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
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
                _logger.LogDebug("Gemini HTTP请求成功，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, API密钥: {ApiKey}, 流式: {IsStreaming}, 分组: {GroupId}({GroupName})",
                    statusCode, elapsedMs, MaskApiKey(apiKey), isStreaming, config.GroupId ?? "未知", config.GroupName ?? "未知");

                var responseStream = await response.Content.ReadAsStreamAsync();

                // 对于流式响应，创建验证包装流
                if (isStreaming)
                {
                    var validatedStream = new GeminiStreamValidationWrapper(responseStream, _logger, apiKey, config, _configuration);
                    return new ProviderHttpResponse
                    {
                        StatusCode = statusCode,
                        IsSuccess = true,
                        ResponseStream = validatedStream,
                        Headers = responseHeaders
                    };
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

                _logger.LogWarning("Gemini HTTP请求失败，状态码: {StatusCode}, 耗时: {ElapsedMs}ms, API密钥: {ApiKey}, 错误: {Error}, 分组: {GroupId}({GroupName})",
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
            _logger.LogError(ex, "Gemini HTTP请求异常，API密钥: {ApiKey}, 分组: {GroupId}({GroupName})", 
                MaskApiKey(apiKey), config.GroupId ?? "未知", config.GroupName ?? "未知");
        }

        _logger.LogError("无法完成Gemini HTTP请求");

        return new ProviderHttpResponse
        {
            StatusCode = 500,
            IsSuccess = false,
            ErrorMessage = lastException?.Message ?? "请求失败",
            ShouldRetry = true,
            ShouldTryNextKey = true
        };
    }

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

    public async Task<ModelsResponse> GetModelsAsync(
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!config.ApiKeys.Any())
            throw new ArgumentException("至少需要一个API密钥", nameof(config));

        Exception? lastException = null;

        foreach (var apiKey in config.ApiKeys)
        {
            try
            {
                var baseUrl = GetBaseUrl(config);
                var endpoint = ModelsEndpoint;
                var fullUrl = $"{baseUrl}{endpoint}";

                using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var geminiResponse = JsonConvert.DeserializeObject<GeminiModelsResponse>(content);

                    return ConvertToModelsResponse(geminiResponse);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var (_, shouldTryNextKey, _) = CheckErrorResponse((int)response.StatusCode, errorContent);

                if (shouldTryNextKey)
                    continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException($"获取模型列表超时 ({config.TimeoutSeconds}秒)");
                _logger.LogWarning("获取 Gemini 模型列表超时, 分组: {GroupId}({GroupName})", 
                    config.GroupId ?? "未知", config.GroupName ?? "未知");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "获取 Gemini 模型列表失败, 分组: {GroupId}({GroupName})", 
                    config.GroupId ?? "未知", config.GroupName ?? "未知");
            }
        }

        // 所有密钥尝试均失败时，不再返回默认模型，抛出异常让上层处理并提示失败
        var reason = lastException?.Message ?? "获取 Gemini 模型列表失败";
        _logger.LogWarning("所有API密钥都无法获取模型列表，将返回错误而非默认模型, 分组: {GroupId}({GroupName}), 原因: {Reason}", 
            config.GroupId ?? "未知", config.GroupName ?? "未知", reason);
        throw lastException ?? new Exception("获取 Gemini 模型列表失败");
        }

    /// <summary>
    /// 获取 Gemini 原生模型列表（带分页支持）
    /// </summary>
    public async Task<GeminiNativeModelsResponse> GetNativeModelsAsync(
        ProviderConfig config,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!config.ApiKeys.Any())
            throw new ArgumentException("至少需要一个API密钥", nameof(config));

        Exception? lastException = null;

        foreach (var apiKey in config.ApiKeys)
        {
            try
            {
                var baseUrl = GetBaseUrl(config);
                var endpoint = ModelsEndpoint;
                var fullUrl = $"{baseUrl}{endpoint}?key={apiKey}";

                // 添加分页Token参数
                if (!string.IsNullOrEmpty(pageToken))
                {
                    fullUrl += $"&pageToken={Uri.EscapeDataString(pageToken)}";
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var geminiResponse = JsonConvert.DeserializeObject<GeminiNativeModelsResponse>(content);

                    return geminiResponse ?? new GeminiNativeModelsResponse();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var (_, shouldTryNextKey, _) = CheckErrorResponse((int)response.StatusCode, errorContent);

                if (shouldTryNextKey)
                    continue;

                // 记录非重试错误
                _logger.LogWarning("获取 Gemini 原生模型列表失败，状态码: {StatusCode}, 错误: {Error}, 分组: {GroupId}({GroupName})",
                    (int)response.StatusCode, errorContent, config.GroupId ?? "未知", config.GroupName ?? "未知");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException($"获取模型列表超时 ({config.TimeoutSeconds}秒)");
                _logger.LogWarning("获取 Gemini 原生模型列表超时, 分组: {GroupId}({GroupName})", 
                    config.GroupId ?? "未知", config.GroupName ?? "未知");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "获取 Gemini 原生模型列表失败, 分组: {GroupId}({GroupName})", 
                    config.GroupId ?? "未知", config.GroupName ?? "未知");
            }
        }

        // 如果所有密钥都失败，抛出异常
        throw lastException ?? new Exception("获取 Gemini 原生模型列表失败");
    }

    public async Task<bool> ValidateApiKeyAsync(
        string apiKey,
        ProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testRequest = new GeminiGenerateContentRequest
            {
                Contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPart> { new GeminiPart { Text = "Hi" } }
                    }
                }
            };

            var content = await PrepareRequestContentAsync(testRequest, config, cancellationToken);
            var response = await SendHttpRequestAsync(content, apiKey, config, false, cancellationToken);

            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验证 Gemini API 密钥失败, 分组: {GroupId}({GroupName})", 
                config.GroupId ?? "未知", config.GroupName ?? "未知");
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
    /// 将ChatCompletionRequest转换为Gemini格式
    /// </summary>
    private GeminiGenerateContentRequest ConvertToGeminiRequest(ChatCompletionRequest request, ProviderConfig config)
    {
        var geminiRequest = new GeminiGenerateContentRequest
        {
            Contents = new List<GeminiContent>()
        };

        foreach (var message in request.Messages)
        {
            var content = new GeminiContent
            {
                Role = message.Role == "assistant" ? "model" : message.Role,
                Parts = new List<GeminiPart>()
            };

            if (message.Content != null)
            {
                var textContent = message.Content.ToString() ?? "";
                content.Parts.Add(new GeminiPart { Text = textContent });
            }

            geminiRequest.Contents.Add(content);
        }

        // 设置生成配置
        if (request.Temperature.HasValue || request.TopP.HasValue || request.MaxTokens.HasValue)
        {
            geminiRequest.GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                MaxOutputTokens = request.MaxTokens
            };
        }

        return geminiRequest;
    }

    /// <summary>
    /// 将Gemini模型响应转换为标准格式
    /// </summary>
    private ModelsResponse ConvertToModelsResponse(GeminiModelsResponse? geminiResponse)
    {
        if (geminiResponse?.Models == null)
        {
            return new ModelsResponse { Data = new List<ModelInfo>() };
        }

        var models = geminiResponse.Models.Select(m => new ModelInfo
        {
            Id = m.Name?.Replace("models/", "") ?? "unknown",
            Object = "model",
            Created = DateTimeOffset.Now.ToUnixTimeSeconds(),
            OwnedBy = "google"
        }).ToList();

        return new ModelsResponse { Data = models };
    }
}

/// <summary>
/// Gemini模型响应类
/// </summary>
public class GeminiModelsResponse
{
    [JsonProperty("models")]
    public List<GeminiModelInfo>? Models { get; set; }
}

/// <summary>
/// Gemini模型信息
/// </summary>
public class GeminiModelInfo
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Gemini流式响应验证包装类
/// </summary>
public class GeminiStreamValidationWrapper : Stream
{
    private readonly Stream _innerStream;
    private readonly ILogger _logger;
    private readonly string _apiKey;
    private readonly ProviderConfig _config;
    private readonly DateTime _startTime;
    private bool _hasReceivedData = false;
    private bool _isComplete = false;
    private int _dataChunkCount = 0;
    private readonly Timer _timeoutTimer;

    private readonly IConfiguration _configuration;

    public GeminiStreamValidationWrapper(Stream innerStream, ILogger logger, string apiKey, ProviderConfig config, IConfiguration configuration)
    {
        _innerStream = innerStream;
        _logger = logger;
        _apiKey = apiKey;
        _config = config;
        _configuration = configuration;
        _startTime = DateTime.UtcNow;
        
        // 从配置读取数据超时时间
        var dataTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Gemini:DataTimeoutSeconds", 30);
        _timeoutTimer = new Timer(CheckDataTimeout, null, TimeSpan.FromSeconds(dataTimeoutSeconds), TimeSpan.FromSeconds(10));
    }

    private void CheckDataTimeout(object? state)
    {
        var elapsed = DateTime.UtcNow - _startTime;
        var dataTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Gemini:DataTimeoutSeconds", 30);
        var maxDataIntervalSeconds = _configuration.GetValue<int>("OrchestrationApi:Gemini:MaxDataIntervalSeconds", 120);
        
        // 如果指定时间内没有收到任何数据，记录警告
        if (!_hasReceivedData && elapsed > TimeSpan.FromSeconds(dataTimeoutSeconds))
        {
            _logger.LogWarning("Gemini流式响应超时未收到数据, API密钥: {ApiKey}, 已等待: {ElapsedSeconds}秒, 分组: {GroupId}", 
                MaskApiKey(_apiKey), elapsed.TotalSeconds, _config.GroupId ?? "未知");
        }
        
        // 如果有数据但长时间没有新数据，也记录警告
        if (_hasReceivedData && !_isComplete && elapsed > TimeSpan.FromSeconds(maxDataIntervalSeconds))
        {
            _logger.LogWarning("Gemini流式响应可能截断, API密钥: {ApiKey}, 已收到数据块: {ChunkCount}, 已等待: {ElapsedSeconds}秒, 分组: {GroupId}", 
                MaskApiKey(_apiKey), _dataChunkCount, elapsed.TotalSeconds, _config.GroupId ?? "未知");
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        
        if (bytesRead > 0)
        {
            _hasReceivedData = true;
            _dataChunkCount++;
            
            // 检查是否是完成标志
            var content = System.Text.Encoding.UTF8.GetString(buffer, offset, bytesRead);
            if (content.Contains("data: [DONE]") || content.Contains("\"finishReason\""))
            {
                _isComplete = true;
                _timeoutTimer?.Dispose();
                
                _logger.LogDebug("Gemini流式响应完成, API密钥: {ApiKey}, 数据块数: {ChunkCount}, 总耗时: {ElapsedSeconds}秒, 分组: {GroupId}", 
                    MaskApiKey(_apiKey), _dataChunkCount, (DateTime.UtcNow - _startTime).TotalSeconds, _config.GroupId ?? "未知");
            }
        }
        else if (bytesRead == 0 && !_isComplete)
        {
            // 流结束但没有收到完成标志，可能是截断
            var elapsed = DateTime.UtcNow - _startTime;
            if (_hasReceivedData)
            {
                _logger.LogWarning("Gemini流式响应可能被截断, API密钥: {ApiKey}, 数据块数: {ChunkCount}, 耗时: {ElapsedSeconds}秒, 分组: {GroupId}", 
                    MaskApiKey(_apiKey), _dataChunkCount, elapsed.TotalSeconds, _config.GroupId ?? "未知");
            }
            else
            {
                _logger.LogWarning("Gemini流式响应空返回, API密钥: {ApiKey}, 耗时: {ElapsedSeconds}秒, 分组: {GroupId}", 
                    MaskApiKey(_apiKey), elapsed.TotalSeconds, _config.GroupId ?? "未知");
            }
        }
        
        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).Result;
    }

    private string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
            return "***";
        return apiKey.Substring(0, 4) + "***" + apiKey.Substring(apiKey.Length - 4);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timeoutTimer?.Dispose();
            _innerStream?.Dispose();
        }
        base.Dispose(disposing);
    }

    // 必需的Stream抽象成员实现
    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position 
    { 
        get => _innerStream.Position; 
        set => _innerStream.Position = value; 
    }
    public override void Flush() => _innerStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => _innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
}
