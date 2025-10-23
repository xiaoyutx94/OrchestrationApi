using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Providers;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 多服务商代理服务接口
/// </summary>
public interface IMultiProviderService
{
    /// <summary>
    /// 验证模型是否有可用的服务商
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>模型验证结果</returns>
    Task<ModelValidationResult> ValidateModelAvailabilityAsync(string model, string proxyKey, string providerType);

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    Task<ModelsResponse> GetAvailableModelsAsync(string proxyKey, string providerType);

    /// <summary>
    /// 获取 Gemini 可用模型列表
    /// </summary>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    Task<GeminiNativeModelsResponse> GetGeminiAvailableModelsAsync(string proxyKey, string providerType);

    /// <summary>
    /// 处理统一的HTTP透传请求（支持OpenAI、Gemini、Anthropic等所有Provider）
    /// </summary>
    /// <param name="requestJson">原始请求JSON字符串</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">Provider类型（可选，null时自动路由到所有类型）</param>
    /// <param name="isStreamRequest">是否为流式请求（可选，仅Gemini需要从路径判断，其他从JSON提取）</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求端点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Provider HTTP响应</returns>
    Task<ProviderHttpResponse> ProcessHttpRequestAsync(
        string requestJson,
        string proxyKey,
        string? providerType = null,
        bool? isStreamRequest = null,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理Responses API请求（透明代理模式）
    /// </summary>
    /// <param name="request">Responses API请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    Task<ProviderHttpResponse> ProcessResponsesHttpAsync(
        string requestJson,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检索之前的响应
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>响应详情</returns>
    Task<object?> RetrieveResponseAsync(string responseId, string proxyKey);

    /// <summary>
    /// 删除存储的响应
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteResponseAsync(string responseId, string proxyKey);

    /// <summary>
    /// 取消后台响应任务
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>取消结果</returns>
    Task<object?> CancelResponseAsync(string responseId, string proxyKey);
}

/// <summary>
/// 多服务商代理服务实现
/// </summary>
public class MultiProviderService : IMultiProviderService
{
    private readonly IProviderRouter _router;
    private readonly IProviderFactory _providerFactory;
    private readonly IKeyManager _keyManager;
    private readonly IRequestLogger _requestLogger;
    private readonly ILogger<MultiProviderService> _logger;
    private readonly IConfiguration _configuration;

    public MultiProviderService(
        IProviderRouter router,
        IProviderFactory providerFactory,
        IKeyManager keyManager,
        IRequestLogger requestLogger,
        ILogger<MultiProviderService> logger,
        IConfiguration configuration)
    {
        _router = router;
        _providerFactory = providerFactory;
        _keyManager = keyManager;
        _requestLogger = requestLogger;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 验证模型可用性
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>模型验证结果</returns>
    public async Task<ModelValidationResult> ValidateModelAvailabilityAsync(string model, string proxyKey, string providerType)
    {
        try
        {
            _logger.LogDebug("开始验证模型可用性 - Model: {Model}, ProxyKey: {ProxyKey}",
                model, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

            // 路由请求以检查是否有可用的服务商
            var routeResult = await _router.RouteRequestAsync(model, proxyKey, providerType);

            if (!routeResult.Success)
            {
                var errorMessage = routeResult.ErrorMessage.Contains("未找到支持模型")
                    ? $"当前代理密钥对于模型 {model} 无可用服务商"
                    : routeResult.ErrorMessage;

                return new ModelValidationResult
                {
                    IsValid = false,
                    ErrorMessage = errorMessage,
                    AvailableProvidersCount = 0
                };
            }

            // 检查是否有可用的API密钥
            var apiKey = await _keyManager.GetNextKeyAsync(routeResult.Group!.Id);
            if (string.IsNullOrEmpty(apiKey))
            {
                return new ModelValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"当前代理密钥对于模型 {model} 无可用服务商",
                    AvailableProvidersCount = 0
                };
            }

            _logger.LogDebug("模型验证成功 - Model: {Model}, Group: {GroupId}", model, routeResult.Group.Id);

            return new ModelValidationResult
            {
                IsValid = true,
                ErrorMessage = string.Empty,
                AvailableProvidersCount = 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证模型可用性时发生异常 - Model: {Model}", model);
            return new ModelValidationResult
            {
                IsValid = false,
                ErrorMessage = $"当前代理密钥对于模型 {model} 无可用服务商",
                AvailableProvidersCount = 0
            };
        }
    }

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>可用模型列表</returns>
    public async Task<ModelsResponse> GetAvailableModelsAsync(string proxyKey, string providerType)
    {
        try
        {
            _logger.LogInformation("获取可用模型列表 - ProxyKey: {ProxyKey}", string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

            var allModels = new List<ModelInfo>();
            var addedModelIds = new HashSet<string>();

            // 获取允许访问的分组
            var allowedGroups = await _router.GetAllowedGroupsAsync(proxyKey, providerType);

            _logger.LogInformation("找到 {GroupCount} 个允许访问的分组", allowedGroups.Count);

            foreach (var group in allowedGroups)
            {
                try
                {
                    // 解析分组配置的模型列表
                    var configuredModels = string.IsNullOrEmpty(group.Models) ?
                        [] :
                        JsonConvert.DeserializeObject<List<string>>(group.Models) ?? [];

                    // 解析模型别名映射
                    var modelAliases = string.IsNullOrEmpty(group.ModelAliases) ?
                        [] :
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();

                    // 如果分组配置了具体的模型列表，使用配置的模型
                    if (configuredModels.Count > 0)
                    {
                        _logger.LogInformation("分组 {GroupId} 配置了 {ModelCount} 个模型: {Models}",
                            group.Id, configuredModels.Count, string.Join(", ", configuredModels));

                        foreach (var modelId in configuredModels)
                        {
                            // 检查是否有别名映射，如果有则使用别名
                            var finalModelId = modelId;
                            var aliasKey = modelAliases.FirstOrDefault(kvp => kvp.Value == modelId).Key;
                            if (!string.IsNullOrEmpty(aliasKey))
                            {
                                finalModelId = aliasKey;
                                _logger.LogDebug("模型 {OriginalModel} 映射为别名 {Alias}", modelId, aliasKey);
                            }

                            // 避免重复添加相同ID的模型
                            if (!addedModelIds.Contains(finalModelId))
                            {
                                allModels.Add(new ModelInfo
                                {
                                    Id = finalModelId,
                                    Object = "model",
                                    Created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                    OwnedBy = group.ProviderType
                                });
                                addedModelIds.Add(finalModelId);
                                _logger.LogDebug("添加模型: {ModelId} (来源: 分组 {GroupId})", finalModelId, group.Id);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有配置具体模型，尝试从服务商获取可用模型
                        var provider = _providerFactory.GetProvider(group.ProviderType);
                        var apiKeys = await _keyManager.GetGroupApiKeysAsync(group.Id);

                        if (apiKeys.Count > 0)
                        {
                            var config = new ProviderConfig
                            {
                                ApiKeys = apiKeys,
                                BaseUrl = string.IsNullOrWhiteSpace(group.BaseUrl) ? null : group.BaseUrl,
                                TimeoutSeconds = group.Timeout, // 向后兼容
                                ConnectionTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30),
                                ResponseTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ResponseTimeout", 300),
                                MaxRetries = group.RetryCount,
                                Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers ?? "{}") ?? new Dictionary<string, string>(),
                                ModelAliases = modelAliases,
                                ParameterOverrides = string.IsNullOrEmpty(group.ParameterOverrides) ? new() : JsonConvert.DeserializeObject<Dictionary<string, object>>(group.ParameterOverrides) ?? new(),
                                GroupId = group.Id,
                                GroupName = group.GroupName,
                                ProxyConfig = ParseProxyConfig(group),
                                FakeStreaming = group.FakeStreaming // 传递假流配置
                            };

                            var providerModels = await provider.GetModelsAsync(config);

                            foreach (var model in providerModels.Data)
                            {
                                // 检查是否有别名映射
                                var finalModelId = model.Id;
                                var aliasKey = modelAliases.FirstOrDefault(kvp => kvp.Value == model.Id).Key;
                                if (!string.IsNullOrEmpty(aliasKey))
                                {
                                    finalModelId = aliasKey;
                                }

                                // 避免重复添加相同ID的模型
                                if (!addedModelIds.Contains(finalModelId))
                                {
                                    allModels.Add(new ModelInfo
                                    {
                                        Id = finalModelId,
                                        Object = model.Object,
                                        Created = model.Created,
                                        OwnedBy = model.OwnedBy
                                    });
                                    addedModelIds.Add(finalModelId);
                                }
                            }
                        }
                    }

                    // 同时添加所有别名作为可用模型
                    foreach (var alias in modelAliases.Keys)
                    {
                        if (!addedModelIds.Contains(alias))
                        {
                            allModels.Add(new ModelInfo
                            {
                                Id = alias,
                                Object = "model",
                                Created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                                OwnedBy = group.ProviderType
                            });
                            addedModelIds.Add(alias);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取分组 {GroupId} ({ProviderType}) 的模型列表时发生异常", group.Id, group.ProviderType);
                }
            }

            var finalModels = allModels.OrderBy(m => m.Id).ToList();
            _logger.LogInformation("返回 {ModelCount} 个可用模型: {Models}",
                finalModels.Count, string.Join(", ", finalModels.Take(10).Select(m => m.Id)));

            return new ModelsResponse
            {
                Data = finalModels
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取可用模型列表时发生异常");
            return new ModelsResponse();
        }
    }

    /// <summary>
    /// 获取 Gemini 可用模型列表
    /// </summary>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>可用模型列表</returns>
    public async Task<GeminiNativeModelsResponse> GetGeminiAvailableModelsAsync(string proxyKey, string providerType)
    {
        try
        {
            _logger.LogInformation("获取 Gemini 可用模型列表 - ProxyKey: {ProxyKey}", string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

            var allModels = new List<GeminiNativeModelInfo>();
            var addedModelIds = new HashSet<string>();

            // 获取允许访问的分组
            var allowedGroups = await _router.GetAllowedGroupsAsync(proxyKey, providerType);

            _logger.LogInformation("找到 {GroupCount} 个允许访问的分组", allowedGroups.Count);

            foreach (var group in allowedGroups)
            {
                try
                {
                    // 解析分组配置的模型列表
                    var configuredModels = string.IsNullOrEmpty(group.Models) ?
                        [] :
                        JsonConvert.DeserializeObject<List<string>>(group.Models) ?? [];

                    // 解析模型别名映射
                    var modelAliases = string.IsNullOrEmpty(group.ModelAliases) ?
                        [] :
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();

                    // 如果分组配置了具体的模型列表，使用配置的模型
                    if (configuredModels.Count > 0)
                    {
                        _logger.LogInformation("分组 {GroupId} 配置了 {ModelCount} 个模型: {Models}",
                            group.Id, configuredModels.Count, string.Join(", ", configuredModels));

                        foreach (var modelId in configuredModels)
                        {
                            // 对于 Gemini，去除 "models/" 前缀
                            var cleanModelId = modelId.StartsWith("models/") ? modelId[7..] : modelId;

                            // 检查是否有别名映射，如果有则使用别名
                            var finalModelId = cleanModelId;
                            var aliasKey = modelAliases.FirstOrDefault(kvp => kvp.Value == cleanModelId).Key;
                            if (!string.IsNullOrEmpty(aliasKey))
                            {
                                finalModelId = aliasKey;
                                _logger.LogDebug("模型 {OriginalModel} 映射为别名 {Alias}", cleanModelId, aliasKey);
                            }

                            // 避免重复添加相同ID的模型
                            if (!addedModelIds.Contains(finalModelId))
                            {
                                allModels.Add(new GeminiNativeModelInfo
                                {
                                    Name = finalModelId,
                                    DisplayName = finalModelId,
                                    Description = $"Gemini model from {group.ProviderType}"
                                });
                                addedModelIds.Add(finalModelId);
                                _logger.LogDebug("添加模型: {ModelId} (来源: 分组 {GroupId})", finalModelId, group.Id);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有配置具体模型，尝试从服务商获取可用模型
                        var provider = _providerFactory.GetProvider(group.ProviderType);
                        var apiKeys = await _keyManager.GetGroupApiKeysAsync(group.Id);

                        if (apiKeys.Count > 0)
                        {
                            var config = new ProviderConfig
                            {
                                ApiKeys = apiKeys,
                                BaseUrl = string.IsNullOrWhiteSpace(group.BaseUrl) ? null : group.BaseUrl,
                                TimeoutSeconds = group.Timeout, // 向后兼容
                                ConnectionTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30),
                                ResponseTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ResponseTimeout", 300),
                                MaxRetries = group.RetryCount,
                                Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers ?? "{}") ?? new Dictionary<string, string>(),
                                ModelAliases = modelAliases,
                                ParameterOverrides = string.IsNullOrEmpty(group.ParameterOverrides) ? new() : JsonConvert.DeserializeObject<Dictionary<string, object>>(group.ParameterOverrides) ?? new(),
                                GroupId = group.Id,
                                GroupName = group.GroupName,
                                ProxyConfig = ParseProxyConfig(group),
                                FakeStreaming = group.FakeStreaming // 传递假流配置
                            };

                            var providerModels = await provider.GetModelsAsync(config);

                            foreach (var model in providerModels.Data)
                            {
                                // 对于 Gemini，去除 "models/" 前缀
                                var cleanModelId = model.Id.StartsWith("models/") ? model.Id.Substring(7) : model.Id;

                                // 检查是否有别名映射
                                var finalModelId = cleanModelId;
                                var aliasKey = modelAliases.FirstOrDefault(kvp => kvp.Value == cleanModelId).Key;
                                if (!string.IsNullOrEmpty(aliasKey))
                                {
                                    finalModelId = aliasKey;
                                }

                                // 避免重复添加相同ID的模型
                                if (!addedModelIds.Contains(finalModelId))
                                {
                                    allModels.Add(new GeminiNativeModelInfo
                                    {
                                        Name = finalModelId,
                                        DisplayName = finalModelId,
                                        Description = $"Gemini model: {finalModelId}"
                                    });
                                    addedModelIds.Add(finalModelId);
                                }
                            }
                        }
                    }

                    // 同时添加所有别名作为可用模型（也要去除 "models/" 前缀）
                    foreach (var alias in modelAliases.Keys)
                    {
                        var cleanAlias = alias.StartsWith("models/") ? alias.Substring(7) : alias;
                        if (!addedModelIds.Contains(cleanAlias))
                        {
                            allModels.Add(new GeminiNativeModelInfo
                            {
                                Name = cleanAlias,
                                DisplayName = cleanAlias,
                                Description = $"Gemini model alias: {cleanAlias}"
                            });
                            addedModelIds.Add(cleanAlias);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取分组 {GroupId} ({ProviderType}) 的模型列表时发生异常", group.Id, group.ProviderType);
                }
            }

            var finalModels = allModels.OrderBy(m => m.Name).ToList();
            _logger.LogInformation("返回 {ModelCount} 个可用的 Gemini 模型: {Models}",
                finalModels.Count, string.Join(", ", finalModels.Take(10).Select(m => m.Name)));

            return new GeminiNativeModelsResponse
            {
                Models = finalModels
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Gemini 可用模型列表时发生异常");
            return new GeminiNativeModelsResponse();
        }
    }

    /// <summary>
    /// 构建提供商配置
    /// </summary>
    /// <param name="routeResult">路由结果</param>
    /// <returns>提供商配置</returns>
    private static ProviderConfig BuildProviderConfig(ProviderRouteResult routeResult)
    {
        var group = routeResult.Group!;

        return new ProviderConfig
        {
            ApiKeys = new List<string> { routeResult.ApiKey! },
            BaseUrl = string.IsNullOrWhiteSpace(group.BaseUrl) ? null : group.BaseUrl,
            TimeoutSeconds = group.Timeout,
            MaxRetries = group.RetryCount,
            Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers ?? "{}") ?? new Dictionary<string, string>(),
            ModelAliases = string.IsNullOrEmpty(group.ModelAliases) ? new() : JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new(),
            ParameterOverrides = string.IsNullOrEmpty(group.ParameterOverrides) ? new() : JsonConvert.DeserializeObject<Dictionary<string, object>>(group.ParameterOverrides) ?? new(),
            GroupId = group.Id,
            GroupName = group.GroupName,
            ProxyConfig = ParseProxyConfig(group),
            FakeStreaming = group.FakeStreaming // 传递假流配置
        };
    }

    /// <summary>
    /// 解析分组的代理配置
    /// </summary>
    /// <param name="group">分组配置</param>
    /// <returns>代理配置</returns>
    private static ProxyConfig? ParseProxyConfig(GroupConfig group)
    {
        if (!group.ProxyEnabled || string.IsNullOrEmpty(group.ProxyConfig))
        {
            return null;
        }

        try
        {
            var proxyConfiguration = JsonConvert.DeserializeObject<ProxyConfiguration>(group.ProxyConfig);
            if (proxyConfiguration != null && !string.IsNullOrEmpty(proxyConfiguration.Host))
            {
                return new ProxyConfig
                {
                    Type = proxyConfiguration.Type,
                    Host = proxyConfiguration.Host,
                    Port = proxyConfiguration.Port,
                    Username = proxyConfiguration.Username,
                    Password = proxyConfiguration.Password,
                    BypassLocal = proxyConfiguration.BypassLocal,
                    BypassDomains = proxyConfiguration.BypassDomains ?? new List<string>()
                };
            }
        }
        catch (Exception)
        {
            // 代理配置解析失败时忽略，使用无代理配置
        }

        return null;
    }

    /// <summary>
    /// 应用参数覆盖到请求字典（用于JSON透传模式）
    /// </summary>
    /// <param name="requestDict">请求参数字典</param>
    /// <param name="overrides">需要覆盖的参数</param>
    private static void ApplyParameterOverridesToDict(Dictionary<string, object> requestDict, Dictionary<string, object> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            // 直接设置字典键值，自动覆盖原有值
            requestDict[key] = value;
        }
    }

    /// <summary>
    /// 根据Provider响应结果决定重试策略
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <param name="exception">异常</param>
    /// <returns>是否需要重试或切换密钥</returns>
    private static (bool shouldRetry, bool shouldSwitchApiKey, bool shouldSwitchProvider) AnalyzeProviderResponse(ProviderHttpResponse response, Exception? exception)
    {
        // 如果响应成功，不需要任何重试
        if (response.IsSuccess)
        {
            return (false, false, false);
        }

        // 如果响应明确指示了重试策略，优先使用响应的建议
        if (response.ShouldTryNextKey)
        {
            return (true, true, false); // 切换API密钥继续当前Provider
        }

        if (response.ShouldRetry && !response.ShouldTryNextKey)
        {
            return (true, false, false); // 使用相同API密钥重试
        }

        // 根据状态码进行分类
        switch (response.StatusCode)
        {
            case 401: // Unauthorized - API key无效
            case 403: // Forbidden - API key权限不足
                return (true, true, false); // 切换API密钥

            case 429: // Rate Limited - 速率限制
                return (true, true, false); // 切换API密钥并重试

            case 500: // Internal Server Error
            case 502: // Bad Gateway
            case 503: // Service Unavailable
            case 504: // Gateway Timeout
                return (true, false, false); // 使用相同密钥重试

            case 400: // Bad Request - 请求格式错误
            case 404: // Not Found
            case 422: // Unprocessable Entity
                return (false, false, true); // 不重试，但可以尝试其他Provider

            default:
                // 未知错误，尝试切换Provider
                return (false, false, true);
        }
    }

    /// <summary>
    /// 根据异常类型决定重试策略
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>是否需要重试或切换密钥</returns>
    private static (bool shouldRetry, bool shouldSwitchApiKey, bool shouldSwitchProvider) AnalyzeException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => httpEx.Message.ToLower() switch
            {
                var msg when msg.Contains("401") || msg.Contains("unauthorized") => (true, true, false),
                var msg when msg.Contains("403") || msg.Contains("forbidden") => (true, true, false),
                var msg when msg.Contains("429") || msg.Contains("rate limit") => (true, true, false),
                var msg when msg.Contains("500") || msg.Contains("502") || msg.Contains("503") || msg.Contains("504") => (true, false, false),
                var msg when msg.Contains("400") || msg.Contains("404") || msg.Contains("422") => (false, false, true),
                _ => (true, false, false) // 默认重试相同密钥
            },
            TaskCanceledException tcEx when tcEx.InnerException is TimeoutException => (true, false, false), // 超时重试
            TaskCanceledException => (false, false, true), // 取消操作，尝试其他Provider
            ArgumentException => (false, false, true), // 参数错误，尝试其他Provider
            InvalidOperationException => (false, false, true), // 操作异常，尝试其他Provider
            _ => (true, false, false) // 默认重试
        };
    }

    /// <summary>
    /// 获取请求的候选服务商分组
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>候选服务商分组</returns>
    private async Task<List<GroupConfig>> GetCandidateGroupsForRequestAsync(string model, string proxyKey, string providerType)
    {
        try
        {
            // 获取所有支持该模型的分组
            var modelGroups = await _router.FindGroupsByModelAsync(model, providerType);
            if (!modelGroups.Any())
            {
                return new List<GroupConfig>();
            }

            // 如果有代理密钥，进一步过滤权限允许的分组
            if (!string.IsNullOrEmpty(proxyKey))
            {
                var allowedGroups = await _router.GetAllowedGroupsAsync(proxyKey, providerType);
                var allowedGroupIds = allowedGroups.Select(g => g.Id).ToHashSet();

                return modelGroups
                    .Where(g => allowedGroupIds.Contains(g.Id))
                    .Where(g => g.Enabled)
                    .ToList();
            }

            return modelGroups.Where(g => g.Enabled).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取候选服务商分组时发生异常 - Model: {Model}", model);
            return new List<GroupConfig>();
        }
    }

    /// <summary>
    /// 服务商分组尝试结果
    /// </summary>
    private class ProviderGroupResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    public async Task<ProviderHttpResponse> ProcessHttpRequestAsync(
        string requestJson,
        string proxyKey,
        string? providerType = null,
        bool? isStreamRequest = null,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        // 解析JSON为字典以便灵活操作
        Dictionary<string, object>? requestDict;
        try
        {
            requestDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestJson);
            if (requestDict == null)
            {
                throw new ArgumentException("Invalid JSON format");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析请求JSON失败");
            return new ProviderHttpResponse
            {
                StatusCode = 400,
                IsSuccess = false,
                ErrorMessage = $"Invalid JSON format: {ex.Message}"
            };
        }

        // 从字典中提取必要的字段
        var originalModelName = requestDict.ContainsKey("model") ? requestDict["model"]?.ToString() ?? string.Empty : string.Empty;

        // 智能提取stream参数：Gemini从参数获取，其他从JSON提取
        var stream = isStreamRequest ?? (requestDict.ContainsKey("stream") && Convert.ToBoolean(requestDict["stream"]));
        var hasTools = requestDict.ContainsKey("tools") && requestDict["tools"] != null;

        // 获取代理密钥ID
        int? proxyKeyId = null;
        if (!string.IsNullOrEmpty(proxyKey))
        {
            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            proxyKeyId = validatedProxyKey?.Id;
        }

        // 记录请求开始
        var requestId = await _requestLogger.LogRequestStartAsync(
            "POST",
            endpoint,
            requestJson, // 直接使用原始JSON字符串
            null, // headers will be added later if needed
            proxyKeyId,
            clientIp,
            userAgent);

        try
        {
            _logger.LogInformation("开始处理HTTP透明代理请求 - RequestId: {RequestId}, Model: {Model}, ProviderType: {ProviderType}",
                requestId, originalModelName, providerType ?? "auto");

            // 获取最大服务商重试个数配置
            var maxProviderRetries = _configuration.GetValue<int>("OrchestrationApi:Global:MaxProviderRetries", 3);
            var triedProviders = new HashSet<string>();
            var failedGroups = new HashSet<string>(); // 记录已失败的分组ID，用于智能降级
            Exception? lastException = null;

            for (int providerAttempt = 0; providerAttempt < maxProviderRetries; providerAttempt++)
            {
                string? currentGroupId = null; // 记录当前尝试的分组ID
                try
                {
                    // 路由请求，传递已失败的分组以实现智能降级
                    var routeResult = await _router.RouteRequestAsync(originalModelName ?? string.Empty, proxyKey, providerType, failedGroups);
                    if (!routeResult.Success)
                    {
                        // 如果路由失败但有失败的分组ID，记录它以避免重复选择
                        if (!string.IsNullOrEmpty(routeResult.FailedGroupId))
                        {
                            failedGroups.Add(routeResult.FailedGroupId);
                            _logger.LogDebug("RequestId: {RequestId}, 路由失败，将分组 {GroupId} 添加到失败列表: {ErrorMessage}",
                                requestId, routeResult.FailedGroupId, routeResult.ErrorMessage);
                        }
                        throw new InvalidOperationException(routeResult.ErrorMessage);
                    }

                    // 记录当前尝试的分组ID
                    currentGroupId = routeResult.Group!.Id;

                    // 检查是否已经尝试过这个服务商
                    var providerKey = $"{routeResult.Group.ProviderType}_{routeResult.Group.Id}";
                    if (triedProviders.Contains(providerKey))
                    {
                        _logger.LogDebug("RequestId: {RequestId}, 已尝试过服务商 {ProviderKey}，跳过",
                            requestId, providerKey);
                        continue;
                    }
                    triedProviders.Add(providerKey);

                    _logger.LogDebug("RequestId: {RequestId}, 尝试服务商 {ProviderType} (分组: {GroupId}) - 第 {AttemptNumber} 次",
                        requestId, routeResult.Group.ProviderType, routeResult.Group.Id, providerAttempt + 1);

                    // 应用参数覆盖（直接操作字典）
                    ApplyParameterOverridesToDict(requestDict, routeResult.ParameterOverrides);

                    // 获取服务商实例
                    var provider = _providerFactory.GetProvider(routeResult.Group.ProviderType);
                    var providerConfig = BuildProviderConfig(routeResult);

                    // 更新模型名称
                    requestDict["model"] = routeResult.ResolvedModel;
                    providerConfig.Model = routeResult.ResolvedModel;

                    // 将修改后的字典序列化为JSON字符串
                    var modifiedRequestJson = JsonConvert.SerializeObject(requestDict);

                    // 准备HTTP请求内容（Provider需要支持接收JSON字符串）
                    var httpContent = await provider.PrepareRequestContentFromJsonAsync(modifiedRequestJson, providerConfig, cancellationToken);

                    // 使用统一的重试策略（移除内部重试循环）
                    var maxRetries = routeResult.Group.RetryCount;
                    for (int attempt = 0; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            // 获取API密钥
                            var currentApiKey = routeResult.ApiKey;
                            if (attempt > 0)
                            {
                                var newKey = await _keyManager.GetNextKeyAsync(routeResult.Group.Id);
                                if (string.IsNullOrEmpty(newKey))
                                {
                                    // 没有更多密钥时，记录失败的分组ID，避免重复选择
                                    if (!failedGroups.Contains(routeResult.Group.Id))
                                    {
                                        failedGroups.Add(routeResult.Group.Id);
                                        _logger.LogDebug("RequestId: {RequestId}, 服务商: {ProviderType}, 分组: {GroupId} 没有更多可用的API密钥，已将分组添加到失败列表",
                                            requestId, routeResult.Group.ProviderType, routeResult.Group.Id);
                                    }
                                    break; // 没有更多密钥，跳出当前Provider的重试循环
                                }
                                currentApiKey = newKey;
                                providerConfig.ApiKeys = [newKey];
                            }

                            _logger.LogDebug("发送HTTP请求 - RequestId: {RequestId}, 服务商: {ProviderType}, 分组: {GroupId}, 尝试: {Attempt}, API密钥: {ApiKey}",
                                requestId, routeResult.Group.ProviderType, routeResult.Group.Id, attempt + 1, currentApiKey?[..8] + "...");

                            // 发送HTTP请求（Provider不再包含重试逻辑）
                            // 注意：这里不能直接传入request.Stream，因为假流模式需要发送非流式请求到上游
                            var actualIsStreaming = providerConfig.FakeStreaming ? false : stream;
                            var response = await provider.SendHttpRequestAsync(
                                httpContent, currentApiKey!, providerConfig, actualIsStreaming, cancellationToken);

                            // 分析响应并决定后续策略
                            var (shouldRetry, shouldSwitchApiKey, shouldSwitchProvider) = AnalyzeProviderResponse(response, null);

                            if (response.IsSuccess)
                            {
                                // 成功 - 重置密钥错误计数
                                await _keyManager.ResetKeyErrorCountAsync(routeResult.Group.Id, currentApiKey!);

                                // 更新密钥使用统计（HTTP透明代理）
                                await _keyManager.UpdateKeyUsageStatsAsync(routeResult.Group.Id, currentApiKey!);

                                // 更新代理密钥使用统计
                                if (!string.IsNullOrEmpty(proxyKey))
                                {
                                    var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
                                    if (validatedProxyKey != null)
                                    {
                                        await _keyManager.UpdateProxyKeyUsageAsync(validatedProxyKey.Id);
                                    }
                                }

                                // 记录成功的请求日志
                                await _requestLogger.LogRequestEndAsync(requestId, response.StatusCode,
                                    null, // HTTP透明代理模式不记录响应体内容，保持透传性能
                                    response.Headers,
                                    null, // no error message
                                    null, null, null, // token信息在HTTP透明代理模式下无法准确获取
                                    routeResult.Group.Id,
                                    routeResult.Group.ProviderType,
                                    originalModelName, // 使用原始模型名称
                                    hasTools,
                                    stream,
                                    currentApiKey);

                                _logger.LogInformation("HTTP透明代理请求成功 - RequestId: {RequestId}, 服务商: {ProviderType}, 尝试次数: {Attempt}",
                                    requestId, routeResult.Group.ProviderType, attempt + 1);

                                return response;
                            }
                            else
                            {
                                // 失败 - 报告密钥错误
                                await _keyManager.ReportKeyErrorAsync(routeResult.Group.Id, currentApiKey!,
                                    response.ErrorMessage ?? "HTTP请求失败");

                                _logger.LogWarning("Provider响应失败 - Provider: {ProviderType}, 状态码: {StatusCode}, 错误: {Error}, 策略: 重试={ShouldRetry}, 切换密钥={ShouldSwitchApiKey}, 切换Provider={ShouldSwitchProvider}",
                                    routeResult.Group.ProviderType, response.StatusCode, response.ErrorMessage, shouldRetry, shouldSwitchApiKey, shouldSwitchProvider);

                                if (shouldSwitchProvider)
                                {
                                    // 需要切换Provider，跳出当前Provider的重试循环
                                    break;
                                }

                                if (!shouldRetry)
                                {
                                    // 记录失败的请求日志
                                    await _requestLogger.LogRequestEndAsync(requestId, response.StatusCode,
                                        null, // HTTP透明代理模式不记录响应体内容
                                        response.Headers,
                                        response.ErrorMessage,
                                        null, null, null, // token信息在HTTP透明代理模式下无法准确获取
                                        routeResult.Group.Id,
                                        routeResult.Group.ProviderType,
                                        originalModelName,
                                        hasTools,
                                        stream,
                                        currentApiKey);

                                    // 不可重试的错误，直接返回
                                    return response;
                                }

                                if (shouldSwitchApiKey)
                                {
                                    // 切换API密钥，继续重试
                                    continue;
                                }

                                // 使用相同密钥重试
                                if (attempt >= maxRetries)
                                {
                                    // 达到最大重试次数，跳出循环
                                    break;
                                }

                                // 等待一段时间再重试（指数退避）
                                var delaySeconds = Math.Min(Math.Pow(2, attempt), 30);
                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            var (shouldRetry, shouldSwitchApiKey, shouldSwitchProvider) = AnalyzeException(ex);

                            _logger.LogWarning(ex, "Provider异常 - Provider: {ProviderType}, 尝试: {Attempt}, 异常: {ExceptionType}, 策略: 重试={ShouldRetry}, 切换密钥={ShouldSwitchApiKey}, 切换Provider={ShouldSwitchProvider}",
                                routeResult.Group.ProviderType, attempt + 1, ex.GetType().Name, shouldRetry, shouldSwitchApiKey, shouldSwitchProvider);

                            // 报告密钥错误
                            await _keyManager.ReportKeyErrorAsync(routeResult.Group.Id, routeResult.ApiKey!, ex.Message);

                            if (shouldSwitchProvider)
                            {
                                // 需要切换Provider，跳出当前Provider的重试循环
                                break;
                            }

                            if (!shouldRetry)
                            {
                                // 不可重试的错误，抛出异常
                                throw;
                            }

                            if (attempt >= maxRetries)
                            {
                                // 达到最大重试次数，跳出循环
                                break;
                            }

                            // 等待一段时间再重试
                            var delaySeconds = Math.Min(Math.Pow(2, attempt), 30);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "处理HTTP透明代理请求时发生异常 - RequestId: {RequestId}, 服务商尝试: {ProviderAttempt}",
                        requestId, providerAttempt + 1);

                    // 将失败的分组添加到失败列表，避免重复选择
                    if (!string.IsNullOrEmpty(currentGroupId))
                    {
                        failedGroups.Add(currentGroupId);
                        _logger.LogDebug("RequestId: {RequestId}, 将分组 {GroupId} 添加到失败列表，避免重复选择",
                            requestId, currentGroupId);
                    }
                }
            }

            // 所有服务商都失败
            var errorMessage = triedProviders.Count >= maxProviderRetries
                ? $"当前代理密钥对于模型 {originalModelName} 无可用服务商"
                : "暂无可用服务商处理请求";

            _logger.LogError("HTTP透明代理请求失败 - RequestId: {RequestId}, 已尝试 {TriedProviders} 个服务商",
                requestId, triedProviders.Count);

            // 记录失败的请求日志
            await _requestLogger.LogRequestEndAsync(requestId, 500,
                null, // no response body
                null, // no response headers
                errorMessage,
                null, null, null, // no token info
                null, // no group id available since all providers failed
                null, // no provider type available
                originalModelName, // 使用原始模型名称
                hasTools,
                stream,
                null); // no openrouter key available since all providers failed

            return new ProviderHttpResponse
            {
                StatusCode = 500,
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理HTTP透明代理请求时发生异常 - RequestId: {RequestId}", requestId);

            // 记录异常的请求日志
            await _requestLogger.LogRequestEndAsync(requestId, 500,
                null, // no response body
                null, // no response headers
                ex.Message,
                null, null, null, // no token info
                null, // no group id available due to exception
                null, // no provider type available
                originalModelName, // 使用原始模型名称
                hasTools,
                stream,
                null); // no openrouter key available due to exception

            return new ProviderHttpResponse
            {
                StatusCode = 500,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 处理Responses API HTTP请求（透明代理模式）
    /// </summary>
    /// <param name="request">Responses API请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    public async Task<ProviderHttpResponse> ProcessResponsesHttpAsync(
        string requestJson,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        // 获取代理密钥ID
        int? proxyKeyId = null;
        if (!string.IsNullOrEmpty(proxyKey))
        {
            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            proxyKeyId = validatedProxyKey?.Id;
        }

        // 从JSON中解析请求字段
        dynamic? requestObj = null;
        string? originalModelName = null;
        bool? stream = null;
        bool? hasTools = null;
        
        try
        {
            requestObj = JsonConvert.DeserializeObject<dynamic>(requestJson);
            originalModelName = requestObj?.model?.ToString();
            stream = requestObj?.stream;
            hasTools = requestObj?.tools != null;
        }
        catch
        {
            // JSON解析失败，使用默认值
        }

        // 记录请求开始
        var requestId = await _requestLogger.LogRequestStartAsync(
            "POST",
            endpoint,
            requestJson,
            null,
            proxyKeyId,
            clientIp,
            userAgent);

        try
        {
            _logger.LogInformation("开始处理Responses API HTTP透明代理请求 - RequestId: {RequestId}, Model: {Model}",
                requestId, originalModelName);

            // 获取最大服务商重试个数配置
            var maxProviderRetries = _configuration.GetValue<int>("OrchestrationApi:Global:MaxProviderRetries", 3);
            var triedProviders = new HashSet<string>();
            var failedGroups = new HashSet<string>();
            Exception? lastException = null;

            for (int providerAttempt = 0; providerAttempt < maxProviderRetries; providerAttempt++)
            {
                string? currentGroupId = null;
                try
                {
                    // 路由请求，传递已失败的分组以实现智能降级
                    var routeResult = await _router.RouteRequestAsync(originalModelName ?? string.Empty, proxyKey, providerType, failedGroups);
                    if (!routeResult.Success)
                    {
                        if (!string.IsNullOrEmpty(routeResult.FailedGroupId))
                        {
                            failedGroups.Add(routeResult.FailedGroupId);
                            _logger.LogDebug("RequestId: {RequestId}, 路由失败，将分组 {GroupId} 添加到失败列表: {ErrorMessage}",
                                requestId, routeResult.FailedGroupId, routeResult.ErrorMessage);
                        }
                        throw new InvalidOperationException(routeResult.ErrorMessage);
                    }

                    currentGroupId = routeResult.Group!.Id;

                    // 检查是否已经尝试过这个服务商
                    var providerKey = $"{routeResult.Group.ProviderType}_{routeResult.Group.Id}";
                    if (triedProviders.Contains(providerKey))
                    {
                        _logger.LogDebug("RequestId: {RequestId}, 已尝试过服务商 {ProviderKey}，跳过",
                            requestId, providerKey);
                        continue;
                    }
                    triedProviders.Add(providerKey);

                    _logger.LogDebug("RequestId: {RequestId}, 尝试服务商 {ProviderType} (分组: {GroupId}) - 第 {AttemptNumber} 次",
                        requestId, routeResult.Group.ProviderType, routeResult.Group.Id, providerAttempt + 1);

                    // 获取服务商实例
                    var provider = _providerFactory.GetProvider(routeResult.Group.ProviderType);
                    var providerConfig = BuildProviderConfig(routeResult);

                    // 设置端点类型为responses
                    providerConfig.EndpointType = "responses";

                    // 准备Responses API请求内容（JSON透传模式）
                    var httpContent = new StringContent(requestJson, Encoding.UTF8);
                    httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    // 使用统一的重试策略
                    var maxRetries = routeResult.Group.RetryCount;
                    for (int attempt = 0; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            // 获取API密钥
                            var currentApiKey = routeResult.ApiKey;
                            if (attempt > 0)
                            {
                                var newKey = await _keyManager.GetNextKeyAsync(routeResult.Group.Id);
                                if (string.IsNullOrEmpty(newKey))
                                {
                                    if (!failedGroups.Contains(routeResult.Group.Id))
                                    {
                                        failedGroups.Add(routeResult.Group.Id);
                                        _logger.LogDebug("RequestId: {RequestId}, 服务商: {ProviderType}, 分组: {GroupId} 没有更多可用的API密钥",
                                            requestId, routeResult.Group.ProviderType, routeResult.Group.Id);
                                    }
                                    break;
                                }
                                currentApiKey = newKey;
                                providerConfig.ApiKeys = [newKey];
                            }

                            _logger.LogDebug("发送Responses HTTP请求 - RequestId: {RequestId}, 服务商: {ProviderType}, 分组: {GroupId}, 尝试: {Attempt}",
                                requestId, routeResult.Group.ProviderType, routeResult.Group.Id, attempt + 1);

                            // 发送HTTP请求
                            // 注意：假流模式需要发送非流式请求到上游
                            var actualIsStreaming = providerConfig.FakeStreaming ? false : (stream == true);
                            var response = await provider.SendHttpRequestAsync(
                                httpContent, currentApiKey!, providerConfig, actualIsStreaming, cancellationToken);

                            var (shouldRetry, shouldSwitchApiKey, shouldSwitchProvider) = AnalyzeProviderResponse(response, null);

                            if (response.IsSuccess)
                            {
                                // 成功处理
                                await _keyManager.ResetKeyErrorCountAsync(routeResult.Group.Id, currentApiKey!);
                                await _keyManager.UpdateKeyUsageStatsAsync(routeResult.Group.Id, currentApiKey!);

                                // 更新代理密钥使用统计
                                if (!string.IsNullOrEmpty(proxyKey))
                                {
                                    var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
                                    if (validatedProxyKey != null)
                                    {
                                        await _keyManager.UpdateProxyKeyUsageAsync(validatedProxyKey.Id);
                                    }
                                }

                                // 记录成功日志
                                await _requestLogger.LogRequestEndAsync(requestId, response.StatusCode,
                                    null, response.Headers, null, null, null, null,
                                    routeResult.Group.Id, routeResult.Group.ProviderType, originalModelName,
                                    hasTools == true, stream == true, currentApiKey);

                                _logger.LogInformation("Responses API HTTP透明代理请求成功 - RequestId: {RequestId}, 服务商: {ProviderType}",
                                    requestId, routeResult.Group.ProviderType);

                                return response;
                            }
                            else
                            {
                                // 失败处理
                                await _keyManager.ReportKeyErrorAsync(routeResult.Group.Id, currentApiKey!,
                                    response.ErrorMessage ?? "HTTP请求失败");

                                _logger.LogWarning("Responses Provider响应失败 - Provider: {ProviderType}, 状态码: {StatusCode}, 错误: {Error}",
                                    routeResult.Group.ProviderType, response.StatusCode, response.ErrorMessage);

                                if (shouldSwitchProvider) break;
                                if (!shouldRetry)
                                {
                                    await _requestLogger.LogRequestEndAsync(requestId, response.StatusCode, null, response.Headers,
                                        response.ErrorMessage, null, null, null, routeResult.Group.Id, routeResult.Group.ProviderType,
                                        originalModelName, hasTools == true, stream == true, currentApiKey);
                                    return response;
                                }

                                if (shouldSwitchApiKey) continue;
                                if (attempt >= maxRetries) break;

                                var delaySeconds = Math.Min(Math.Pow(2, attempt), 30);
                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            var (shouldRetry, shouldSwitchApiKey, shouldSwitchProvider) = AnalyzeException(ex);

                            _logger.LogWarning(ex, "Responses Provider异常 - Provider: {ProviderType}, 尝试: {Attempt}",
                                routeResult.Group.ProviderType, attempt + 1);

                            await _keyManager.ReportKeyErrorAsync(routeResult.Group.Id, routeResult.ApiKey!, ex.Message);

                            if (shouldSwitchProvider) break;
                            if (!shouldRetry) throw;
                            if (attempt >= maxRetries) break;

                            var delaySeconds = Math.Min(Math.Pow(2, attempt), 30);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "处理Responses API HTTP透明代理请求时发生异常 - RequestId: {RequestId}, 服务商尝试: {ProviderAttempt}",
                        requestId, providerAttempt + 1);

                    if (!string.IsNullOrEmpty(currentGroupId))
                    {
                        failedGroups.Add(currentGroupId);
                    }
                }
            }

            // 所有服务商都失败
            var errorMessage = triedProviders.Count >= maxProviderRetries
                ? $"当前代理密钥对于模型 {originalModelName} 无可用服务商"
                : "暂无可用服务商处理Responses请求";

            _logger.LogError("Responses API HTTP透明代理请求失败 - RequestId: {RequestId}, 已尝试 {TriedProviders} 个服务商",
                requestId, triedProviders.Count);

            await _requestLogger.LogRequestEndAsync(requestId, 500, null, null, errorMessage,
                null, null, null, null, null, originalModelName,
                hasTools == true, stream == true, null);

            return new ProviderHttpResponse
            {
                StatusCode = 500,
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            var errorMessage = $"Responses API请求处理异常: {ex.Message}";
            _logger.LogError(ex, errorMessage + " - RequestId: {RequestId}", requestId);
            await _requestLogger.LogRequestEndAsync(requestId, 500, null, null, errorMessage);

            return new ProviderHttpResponse
            {
                IsSuccess = false,
                StatusCode = 500,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// 检索之前的响应
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>响应详情</returns>
    public async Task<object?> RetrieveResponseAsync(string responseId, string proxyKey)
    {
        _logger.LogDebug("检索响应 - ResponseId: {ResponseId}, ProxyKey: {ProxyKey}",
            responseId, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

        try
        {
            // 验证代理密钥
            if (string.IsNullOrEmpty(proxyKey))
            {
                throw new ArgumentException("ProxyKey is required");
            }

            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            if (validatedProxyKey == null)
            {
                throw new UnauthorizedAccessException("Invalid ProxyKey");
            }

            // 路由到支持Responses API的Provider
            var routeResult = await _router.RouteRequestAsync("responses", proxyKey, "openai");
            if (!routeResult.Success)
            {
                _logger.LogWarning("路由响应检索请求失败: {ErrorMessage}", routeResult.ErrorMessage);
                return null;
            }

            // 获取Provider实例
            var provider = _providerFactory.GetProvider(routeResult.Group!.ProviderType);
            var providerConfig = BuildProviderConfig(routeResult);

            // 构建检索请求
            var retrieveRequest = new
            {
                response_id = responseId
            };

            // 准备HTTP请求内容
            var httpContent = new StringContent(
                JsonConvert.SerializeObject(retrieveRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            // 发送HTTP请求到Provider
            var response = await provider.SendHttpRequestAsync(
                httpContent, routeResult.ApiKey!, providerConfig, false, CancellationToken.None);

            if (response.IsSuccess && response.ResponseStream != null)
            {
                // 读取响应流内容
                using var reader = new StreamReader(response.ResponseStream);
                var responseBody = await reader.ReadToEndAsync();

                if (!string.IsNullOrEmpty(responseBody))
                {
                    // 解析响应并返回
                    var apiResponse = JsonConvert.DeserializeObject<object>(responseBody);
                    _logger.LogInformation("成功检索响应 - ResponseId: {ResponseId}", responseId);
                    return apiResponse;
                }
            }

            _logger.LogWarning("检索响应失败 - ResponseId: {ResponseId}, StatusCode: {StatusCode}, Error: {Error}",
                responseId, response.StatusCode, response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索响应时发生异常 - ResponseId: {ResponseId}", responseId);
            return null;
        }
    }

    /// <summary>
    /// 删除存储的响应
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> DeleteResponseAsync(string responseId, string proxyKey)
    {
        _logger.LogDebug("删除响应 - ResponseId: {ResponseId}, ProxyKey: {ProxyKey}",
            responseId, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

        try
        {
            // 验证代理密钥
            if (string.IsNullOrEmpty(proxyKey))
            {
                throw new ArgumentException("ProxyKey is required");
            }

            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            if (validatedProxyKey == null)
            {
                throw new UnauthorizedAccessException("Invalid ProxyKey");
            }

            // 路由到支持Responses API的Provider
            var routeResult = await _router.RouteRequestAsync("responses", proxyKey, "openai");
            if (!routeResult.Success)
            {
                _logger.LogWarning("路由响应删除请求失败: {ErrorMessage}", routeResult.ErrorMessage);
                return false;
            }

            // 获取Provider实例
            var provider = _providerFactory.GetProvider(routeResult.Group!.ProviderType);
            var providerConfig = BuildProviderConfig(routeResult);

            // 构建删除请求
            var deleteRequest = new
            {
                response_id = responseId
            };

            // 准备HTTP请求内容
            var httpContent = new StringContent(
                JsonConvert.SerializeObject(deleteRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            // 发送HTTP DELETE请求到Provider
            var response = await provider.SendHttpRequestAsync(
                httpContent, routeResult.ApiKey!, providerConfig, false, CancellationToken.None);

            if (response.IsSuccess)
            {
                _logger.LogInformation("成功删除响应 - ResponseId: {ResponseId}", responseId);
                return true;
            }
            else
            {
                _logger.LogWarning("删除响应失败 - ResponseId: {ResponseId}, StatusCode: {StatusCode}, Error: {Error}",
                    responseId, response.StatusCode, response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除响应时发生异常 - ResponseId: {ResponseId}", responseId);
            return false;
        }
    }

    /// <summary>
    /// 取消后台响应任务
    /// </summary>
    /// <param name="responseId">响应ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>取消结果</returns>
    public async Task<object?> CancelResponseAsync(string responseId, string proxyKey)
    {
        _logger.LogDebug("取消响应 - ResponseId: {ResponseId}, ProxyKey: {ProxyKey}",
            responseId, string.IsNullOrEmpty(proxyKey) ? "无" : "已提供");

        try
        {
            // 验证代理密钥
            if (string.IsNullOrEmpty(proxyKey))
            {
                throw new ArgumentException("ProxyKey is required");
            }

            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            if (validatedProxyKey == null)
            {
                throw new UnauthorizedAccessException("Invalid ProxyKey");
            }

            // 路由到支持Responses API的Provider
            var routeResult = await _router.RouteRequestAsync("responses", proxyKey, "openai");
            if (!routeResult.Success)
            {
                _logger.LogWarning("路由响应取消请求失败: {ErrorMessage}", routeResult.ErrorMessage);
                return null;
            }

            // 获取Provider实例
            var provider = _providerFactory.GetProvider(routeResult.Group!.ProviderType);
            var providerConfig = BuildProviderConfig(routeResult);

            // 构建取消请求
            var cancelRequest = new
            {
                response_id = responseId,
                action = "cancel"
            };

            // 准备HTTP请求内容
            var httpContent = new StringContent(
                JsonConvert.SerializeObject(cancelRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            // 发送HTTP POST请求到Provider进行取消操作
            var response = await provider.SendHttpRequestAsync(
                httpContent, routeResult.ApiKey!, providerConfig, false, CancellationToken.None);

            if (response.IsSuccess && response.ResponseStream != null)
            {
                // 读取响应流内容
                using var reader = new StreamReader(response.ResponseStream);
                var responseBody = await reader.ReadToEndAsync();

                if (!string.IsNullOrEmpty(responseBody))
                {
                    // 解析响应并返回
                    var apiResponse = JsonConvert.DeserializeObject<object>(responseBody);
                    _logger.LogInformation("成功取消响应 - ResponseId: {ResponseId}", responseId);
                    return apiResponse;
                }
            }

            _logger.LogWarning("取消响应失败 - ResponseId: {ResponseId}, StatusCode: {StatusCode}, Error: {Error}",
                responseId, response.StatusCode, response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消响应时发生异常 - ResponseId: {ResponseId}", responseId);
            return null;
        }
    }
}