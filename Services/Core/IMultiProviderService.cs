using Newtonsoft.Json;
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
    /// 处理流式聊天完成请求
    /// </summary>
    /// <param name="request">聊天完成请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    IAsyncEnumerable<string> ProcessChatCompletionStreamAsync(
        ChatCompletionRequest request,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理聊天完成HTTP请求（透明代理模式）
    /// </summary>
    /// <param name="request">聊天完成请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    Task<ProviderHttpResponse> ProcessChatCompletionHttpAsync(
        ChatCompletionRequest request,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理Gemini HTTP请求（透明代理模式）
    /// </summary>
    /// <param name="request">Gemini请求</param>
    /// <param name="isStreamRequest">是否流式请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    Task<ProviderHttpResponse> ProcessGeminiHttpRequestAsync(
        GeminiGenerateContentRequest request,
        bool isStreamRequest,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default);

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
    /// 处理Anthropic原生HTTP请求（透明代理模式）
    /// </summary>
    /// <param name="request">Anthropic原生消息请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    Task<ProviderHttpResponse> ProcessAnthropicRequestAsync(
        AnthropicMessageRequest request,
        string proxyKey,
        string? clientIp = null,
        string? userAgent = null,
        string? endpoint = null,
        CancellationToken cancellationToken = default);
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
    /// 处理流式聊天完成请求
    /// </summary>
    /// <param name="request">聊天完成请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    public async IAsyncEnumerable<string> ProcessChatCompletionStreamAsync(
        ChatCompletionRequest request,
        string proxyKey,
        string providerType,
        string? clientIp = null,
        string? userAgent = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var chunks = new List<string>();

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
            "/v1/chat/completions",
            JsonConvert.SerializeObject(request),
            null, // headers will be added later if needed
            proxyKeyId,
            clientIp,
            userAgent);

        // 保存原始模型名称用于日志记录
        var originalModelName = request.Model;

        _logger.LogInformation("开始处理流式聊天完成请求 - RequestId: {RequestId}, Model: {Model}",
            requestId, request.Model);

        // 1. 获取候选服务商
        var candidateGroups = await GetCandidateGroupsForRequestAsync(request.Model, proxyKey, providerType);
        if (!candidateGroups.Any())
        {
            var errorMessage = $"当前代理密钥对于模型 {request.Model} 无可用服务商";
            yield return $"data: {{\"error\":{{\"message\":\"{errorMessage}\",\"type\":\"provider_error\",\"code\":\"no_available_provider\"}}}}\n\n";
            yield break;
        }

        // 2. 按优先级排序并逐个尝试
        var sortedGroups = candidateGroups.OrderByDescending(g => g.Priority).ToList();

        foreach (var group in sortedGroups)
        {
            // 检查权限
            if (!await _router.CheckGroupPermissionAsync(group.Id, proxyKey, providerType))
            {
                _logger.LogDebug("分组 {GroupId} 权限检查失败", group.Id);
                continue;
            }

            // 尝试该服务商，如果成功就直接返回数据
            var hasData = false;

            // 这里直接调用底层Provider，避免复杂的包装
            await foreach (var chunk in TryProviderGroupDirectAsync(group, request, requestId, proxyKey, clientIp, userAgent, startTime, chunks, originalModelName, cancellationToken))
            {
                hasData = true;
                yield return chunk;
            }

            if (hasData)
            {
                // 成功了就直接结束
                _logger.LogInformation("流式请求成功 - RequestId: {RequestId}, Provider: {ProviderType}", requestId, group.ProviderType);
                yield break;
            }
        }

        // 所有服务商都失败
        var finalErrorMessage = $"当前代理密钥对于模型 {request.Model} 无可用服务商";
        yield return $"data: {{\"error\":{{\"message\":\"{finalErrorMessage}\",\"type\":\"provider_error\",\"code\":\"all_providers_failed\"}}}}\n\n";
    }

    /// <summary>
    /// 直接尝试服务商分组，返回实时数据
    /// </summary>
    /// <param name="group">服务商分组</param>
    /// <param name="request">聊天完成请求</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="chunks">数据块列表</param>
    /// <param name="originalModelName">原始模型名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据块列表</returns>
    private async IAsyncEnumerable<string> TryProviderGroupDirectAsync(
        GroupConfig group,
        ChatCompletionRequest request,
        string requestId,
        string? proxyKey,
        string? clientIp,
        string? userAgent,
        DateTime startTime,
        List<string> chunks,
        string originalModelName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 解析模型别名
        var modelAliases = string.IsNullOrEmpty(group.ModelAliases) ?
            new Dictionary<string, string>() :
            JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();

        var resolvedModel = _router.ResolveModelAlias(request.Model, modelAliases);

        // 解析参数覆盖
        var parameterOverrides = string.IsNullOrEmpty(group.ParameterOverrides) ?
            new Dictionary<string, object>() :
            JsonConvert.DeserializeObject<Dictionary<string, object>>(group.ParameterOverrides) ?? new Dictionary<string, object>();

        // 应用参数覆盖到请求
        var originalModel = request.Model;
        ApplyParameterOverrides(request, parameterOverrides);
        request.Model = resolvedModel;

        var provider = _providerFactory.GetProvider(group.ProviderType);
        var maxRetries = group.RetryCount;

        // 在该服务商分组内进行重试（换API key）
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            string? apiKey = null;

            // 获取API密钥
            apiKey = await _keyManager.GetNextKeyAsync(group.Id);
            if (string.IsNullOrEmpty(apiKey))
            {
                break; // 没有密钥了
            }

            // 构建服务商配置
            var providerConfig = new ProviderConfig
            {
                ApiKeys = new List<string> { apiKey },
                BaseUrl = string.IsNullOrWhiteSpace(group.BaseUrl) ? null : group.BaseUrl,
                TimeoutSeconds = group.Timeout, // 向后兼容
                ConnectionTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30),
                ResponseTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ResponseTimeout", 300),
                MaxRetries = group.RetryCount,
                Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers ?? "{}") ?? new Dictionary<string, string>(),
                ModelAliases = modelAliases,
                ParameterOverrides = parameterOverrides,
                GroupId = group.Id,
                GroupName = group.GroupName,
                ProxyConfig = ParseProxyConfig(group)
            };

            _logger.LogDebug("直接调用Provider - RequestId: {RequestId}, Provider: {ProviderType}, GroupId: {GroupId}, Attempt: {Attempt}",
                requestId, group.ProviderType, group.Id, attempt + 1);

            // 直接调用Provider的流式方法
            bool currentAttemptSucceeded = false;

            // 直接调用Provider的HTTP代理方法进行流式处理
            var httpContent = await provider.PrepareRequestContentAsync(request, providerConfig, cancellationToken);
            var httpResponse = await provider.SendHttpRequestAsync(
                httpContent, apiKey, providerConfig, true, cancellationToken);

            if (httpResponse.IsSuccess && httpResponse.ResponseStream != null)
            {
                // 直接透传流式数据，不进行质量检测
                using var reader = new StreamReader(httpResponse.ResponseStream);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    yield return line + "\n";
                    currentAttemptSucceeded = true; // 有数据就算成功
                }
            }
            else if (!httpResponse.IsSuccess)
            {
                _logger.LogWarning("流式请求失败 - Provider: {ProviderType}, 状态码: {StatusCode}, API密钥: {ApiKey}",
                    group.ProviderType, httpResponse.StatusCode,
                    apiKey?.Substring(0, Math.Min(8, apiKey.Length)) + "...");
            }

            if (currentAttemptSucceeded && !string.IsNullOrEmpty(apiKey))
            {
                // 成功了，记录日志并退出
                await _keyManager.ResetKeyErrorCountAsync(group.Id, apiKey);

                // 更新密钥使用统计（流式请求）
                await _keyManager.UpdateKeyUsageStatsAsync(group.Id, apiKey);

                // 记录成功日志
                var routeResult = new ProviderRouteResult
                {
                    Group = group,
                    ApiKey = apiKey,
                    ResolvedModel = resolvedModel,
                    ParameterOverrides = parameterOverrides
                };

                // 记录成功的流式请求日志
                await _requestLogger.LogRequestEndAsync(requestId, 200,
                    null, // 流式请求不记录响应体内容
                    null, // response headers
                    null, // no error message
                    0, // 流式请求默认为0，因为通常无法准确计算
                    0, // 流式请求默认为0
                    0, // 流式请求默认为0
                    routeResult.Group?.Id,
                    routeResult.Group?.ProviderType,
                    originalModelName, // 使用原始模型名称
                    request.Tools?.Any() == true,
                    true, // is streaming
                    apiKey);

                _logger.LogDebug("Provider调用成功 - RequestId: {RequestId}, Provider: {ProviderType}", requestId, group.ProviderType);
                request.Model = originalModel; // 恢复
                yield break;
            }
        }

        // 恢复原始模型名称
        request.Model = originalModel;
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
                                ProxyConfig = ParseProxyConfig(group)
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
                                ProxyConfig = ParseProxyConfig(group)
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
            ProxyConfig = ParseProxyConfig(group)
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
    /// 应用参数覆盖
    /// </summary>
    /// <param name="request">聊天完成请求</param>
    /// <param name="overrides">参数覆盖</param>
    private static void ApplyParameterOverrides(ChatCompletionRequest request, Dictionary<string, object> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            switch (key.ToLower())
            {
                case "temperature":
                    if (value is double temp) request.Temperature = (float)temp;
                    break;

                case "max_tokens":
                    if (value is int maxTokens) request.MaxTokens = maxTokens;
                    break;

                case "top_p":
                    if (value is double topP) request.TopP = (float)topP;
                    break;

                case "presence_penalty":
                    if (value is double presencePenalty) request.PresencePenalty = (float)presencePenalty;
                    break;

                case "frequency_penalty":
                    if (value is double frequencyPenalty) request.FrequencyPenalty = (float)frequencyPenalty;
                    break;
            }
        }
    }

    /// <summary>
    /// 应用参数覆盖
    /// </summary>
    /// <param name="request">Gemini请求</param>
    /// <param name="overrides">参数覆盖</param>
    private static void ApplyParameterOverrides(GeminiGenerateContentRequest request, Dictionary<string, object> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            switch (key.ToLower())
            {
                case "temperature":
                    if (value is double temp)
                    {
                        request.GenerationConfig ??= new GeminiGenerationConfig();
                        request.GenerationConfig.Temperature = (float)temp;
                    }
                    break;

                case "max_tokens":
                    if (value is int maxTokens)
                    {
                        request.GenerationConfig ??= new GeminiGenerationConfig();
                        request.GenerationConfig.MaxOutputTokens = maxTokens;
                    }
                    break;

                case "top_p":
                    if (value is double topP)
                    {
                        request.GenerationConfig ??= new GeminiGenerationConfig();
                        request.GenerationConfig.TopP = (float)topP;
                    }
                    break;
            }
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

    /// <summary>
    /// 处理HTTP透明代理聊天完成请求
    /// </summary>
    /// <param name="request">聊天完成请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    public async Task<ProviderHttpResponse> ProcessChatCompletionHttpAsync(
        ChatCompletionRequest request,
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

        // 保存原始模型名称用于日志记录
        var originalModelName = request.Model;

        // 记录请求开始
        var requestId = await _requestLogger.LogRequestStartAsync(
            "POST",
            endpoint,
            JsonConvert.SerializeObject(request),
            null, // headers will be added later if needed
            proxyKeyId,
            clientIp,
            userAgent);

        try
        {
            _logger.LogInformation("开始处理HTTP透明代理聊天完成请求 - RequestId: {RequestId}, Model: {Model}",
                requestId, request.Model);

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
                    var routeResult = await _router.RouteRequestAsync(originalModelName, proxyKey, providerType, failedGroups);
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

                    // 应用参数覆盖
                    ApplyParameterOverrides(request, routeResult.ParameterOverrides);
                    request.Model = routeResult.ResolvedModel;

                    // 获取服务商实例
                    var provider = _providerFactory.GetProvider(routeResult.Group.ProviderType);
                    var providerConfig = BuildProviderConfig(routeResult);

                    // 准备HTTP请求内容
                    var httpContent = await provider.PrepareRequestContentAsync(request, providerConfig, cancellationToken);

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
                            var response = await provider.SendHttpRequestAsync(
                                httpContent, currentApiKey!, providerConfig, request.Stream, cancellationToken);

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
                                    request.Tools?.Any() == true,
                                    request.Stream,
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
                                        request.Model,
                                        request.Tools?.Any() == true,
                                        request.Stream,
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
                request.Tools?.Any() == true,
                request.Stream,
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
                request.Tools?.Any() == true,
                request.Stream,
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
    /// 处理Gemini HTTP请求（透明代理模式）
    /// <param name="request">Gemini请求</param>
    /// <param name="isStreamRequest">是否流式请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    /// </summary>
    public async Task<ProviderHttpResponse> ProcessGeminiHttpRequestAsync(
        GeminiGenerateContentRequest request,
        bool isStreamRequest,
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

        // 保存原始模型名称用于日志记录
        var originalModelName = request.Model;

        // 记录请求开始
        var requestId = await _requestLogger.LogRequestStartAsync(
            "POST",
            endpoint,
            JsonConvert.SerializeObject(request),
            null, // headers will be added later if needed
            proxyKeyId,
            clientIp,
            userAgent);

        try
        {
            _logger.LogInformation("开始处理Gemini HTTP透明代理请求 - RequestId: {RequestId}",
                requestId);

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
                    // 根据Gemini服务商类型路由请求，传递已失败的分组以实现智能降级
                    var routeResult = await _router.RouteRequestAsync(originalModelName, proxyKey, providerType, failedGroups);
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

                    // 应用参数覆盖
                    ApplyParameterOverrides(request, routeResult.ParameterOverrides);
                    request.Model = routeResult.ResolvedModel;

                    // 获取服务商实例
                    var provider = _providerFactory.GetProvider(routeResult.Group.ProviderType);
                    var providerConfig = BuildProviderConfig(routeResult);
                    providerConfig.Model = request.Model;

                    // 准备HTTP请求内容 - 使用Gemini专用方法
                    HttpContent httpContent = await ((GeminiProvider)provider).PrepareRequestContentAsync(request, providerConfig, cancellationToken);

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
                            var response = await provider.SendHttpRequestAsync(
                                httpContent, currentApiKey!, providerConfig, isStreamRequest, cancellationToken);

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
                                    originalModelName, // 使用原始模型名称而不是硬编码的"gemini"
                                    false, // Gemini请求通常没有tools
                                    isStreamRequest,
                                    currentApiKey);

                                _logger.LogInformation("Gemini HTTP透明代理请求成功 - RequestId: {RequestId}, 服务商: {ProviderType}, 尝试次数: {Attempt}",
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
                                        originalModelName, // 使用原始模型名称而不是硬编码的"gemini"
                                        false, // Gemini请求通常没有tools
                                        isStreamRequest,
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
                    _logger.LogWarning(ex, "处理Gemini HTTP透明代理请求时发生异常 - RequestId: {RequestId}, 服务商尝试: {ProviderAttempt}",
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
                ? "当前代理密钥对于Gemini服务商无可用实例"
                : "暂无可用Gemini服务商处理请求";

            _logger.LogError("Gemini HTTP透明代理请求失败 - RequestId: {RequestId}, 已尝试 {TriedProviders} 个服务商",
                requestId, triedProviders.Count);

            // 记录失败的请求日志
            await _requestLogger.LogRequestEndAsync(requestId, 500,
                null, // no response body
                null, // no response headers
                errorMessage,
                null, null, null, // no token info
                null, // no group id available since all providers failed
                null, // no provider type available
                originalModelName, // 使用原始模型名称而不是硬编码的"gemini"
                false, // Gemini请求通常没有tools
                isStreamRequest,
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
            _logger.LogError(ex, "处理Gemini HTTP透明代理请求时发生异常 - RequestId: {RequestId}", requestId);

            // 记录异常的请求日志
            await _requestLogger.LogRequestEndAsync(requestId, 500,
                null, // no response body
                null, // no response headers
                ex.Message,
                null, null, null, // no token info
                null, // no group id available due to exception
                null, // no provider type available
                originalModelName, // 使用原始模型名称而不是硬编码的"gemini"
                false, // Gemini请求通常没有tools
                isStreamRequest,
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
    /// 处理Anthropic原生HTTP请求（透明代理模式）
    /// </summary>
    /// <param name="request">Anthropic请求</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="clientIp">客户端IP</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="endpoint">请求路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    public async Task<ProviderHttpResponse> ProcessAnthropicRequestAsync(
        AnthropicMessageRequest request,
        string proxyKey,
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

        // 保存原始模型名称用于日志记录
        var originalModelName = request.Model;

        // 记录请求开始
        var requestId = await _requestLogger.LogRequestStartAsync(
            "POST",
            endpoint,
            JsonConvert.SerializeObject(request),
            null,
            proxyKeyId,
            clientIp,
            userAgent);

        try
        {
            _logger.LogInformation("开始处理Anthropic原生HTTP透明代理请求 - RequestId: {RequestId}",
                requestId);

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
                    // 路由到Anthropic Provider
                    var routeResult = await _router.RouteRequestAsync(originalModelName, proxyKey, "anthropic", failedGroups);
                    if (!routeResult.Success)
                    {
                        var errorMessage = $"路由失败: {routeResult.ErrorMessage}";
                        _logger.LogWarning(errorMessage);
                        await _requestLogger.LogRequestEndAsync(requestId, 400, null, null, errorMessage);
                        return new ProviderHttpResponse
                        {
                            IsSuccess = false,
                            StatusCode = 400,
                            ErrorMessage = errorMessage
                        };
                    }

                    var group = routeResult.Group!;
                    var apiKey = routeResult.ApiKey!;
                    var resolvedModel = routeResult.ResolvedModel;
                    var parameterOverrides = routeResult.ParameterOverrides;

                    currentGroupId = group.Id;
                    _logger.LogInformation("Anthropic原生请求路由到分组 {GroupId} (Provider: {ProviderType}) - RequestId: {RequestId}",
                        group.Id, group.ProviderType, requestId);

                    // 创建Provider实例
                    var provider = _providerFactory.GetProvider(group.ProviderType);
                    if (provider == null)
                    {
                        var errorMessage = $"不支持的Provider类型: {group.ProviderType}";
                        _logger.LogError(errorMessage);
                        await _requestLogger.LogRequestEndAsync(requestId, 500, null, null, errorMessage);
                        return new ProviderHttpResponse
                        {
                            IsSuccess = false,
                            StatusCode = 500,
                            ErrorMessage = errorMessage
                        };
                    }

                    // 应用模型解析和参数覆盖
                    var processedRequest = new AnthropicMessageRequest
                    {
                        Model = resolvedModel,
                        Messages = request.Messages,
                        System = request.System,
                        MaxTokens = request.MaxTokens,
                        Temperature = request.Temperature,
                        TopP = request.TopP,
                        TopK = request.TopK,
                        Stream = request.Stream,
                        StopSequences = request.StopSequences
                    };

                    // 参数覆盖将在AnthropicProvider中处理

                    // 构建Provider配置
                    var providerConfig = new ProviderConfig
                    {
                        ApiKeys = await _keyManager.GetGroupApiKeysAsync(group.Id),
                        BaseUrl = group.BaseUrl,
                        TimeoutSeconds = group.Timeout, // 向后兼容
                ConnectionTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30),
                ResponseTimeoutSeconds = _configuration.GetValue<int>("OrchestrationApi:Global:ResponseTimeout", 300),
                        MaxRetries = group.RetryCount,
                        Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers ?? "{}") ?? new Dictionary<string, string>(),
                        ModelAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases ?? "{}") ?? new Dictionary<string, string>(),
                        ParameterOverrides = parameterOverrides,
                        GroupId = group.Id,
                        GroupName = group.GroupName,
                        ProxyConfig = ParseProxyConfig(group)
                    };

                    _logger.LogDebug("直接调用AnthropicProvider - RequestId: {RequestId}, Provider: {ProviderType}, GroupId: {GroupId}",
                        requestId, group.ProviderType, group.Id);

                    // 准备Anthropic原生请求内容
                    var httpContent = await ((AnthropicProvider)provider).PrepareAnthropicRequestContentAsync(processedRequest, providerConfig, cancellationToken);
                    var httpResponse = await provider.SendHttpRequestAsync(
                        httpContent, apiKey, providerConfig, processedRequest.Stream, cancellationToken);

                    if (httpResponse.IsSuccess)
                    {
                        // 成功响应，重置密钥错误计数
                        await _keyManager.ResetKeyErrorCountAsync(group.Id, apiKey);
                        await _keyManager.UpdateKeyUsageStatsAsync(group.Id, apiKey);

                        // 记录成功日志
                        var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var endTime = startTime + 100; // 估算
                        await _requestLogger.LogRequestEndAsync(requestId, httpResponse.StatusCode, null, null, null);

                        return httpResponse;
                    }
                    else
                    {
                        // 请求失败，记录错误
                        await _keyManager.ReportKeyErrorAsync(group.Id, apiKey, httpResponse.ErrorMessage ?? "Unknown error");

                        if (httpResponse.ShouldTryNextKey || httpResponse.ShouldRetry)
                        {
                            failedGroups.Add(group.Id);
                            _logger.LogWarning("Anthropic原生请求失败，将重试 - Provider: {ProviderType}, 状态码: {StatusCode}, 错误: {Error}",
                                group.ProviderType, httpResponse.StatusCode, httpResponse.ErrorMessage);
                            continue;
                        }

                        // 不可重试的错误，直接返回
                        await _requestLogger.LogRequestEndAsync(requestId, httpResponse.StatusCode, null, null, httpResponse.ErrorMessage);
                        return httpResponse;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(ex, "Anthropic原生请求处理异常 - RequestId: {RequestId}, Provider尝试: {Attempt}",
                        requestId, providerAttempt + 1);

                    if (!string.IsNullOrEmpty(currentGroupId))
                    {
                        failedGroups.Add(currentGroupId);
                    }
                }
            }

            // 所有重试都失败了
            var finalErrorMessage = $"所有Provider重试均失败。最后异常: {lastException?.Message}";
            _logger.LogError("Anthropic原生请求最终失败 - RequestId: {RequestId}, 错误: {Error}", requestId, finalErrorMessage);
            await _requestLogger.LogRequestEndAsync(requestId, 500, null, null, finalErrorMessage);

            return new ProviderHttpResponse
            {
                IsSuccess = false,
                StatusCode = 500,
                ErrorMessage = finalErrorMessage
            };
        }
        catch (Exception ex)
        {
            var errorMessage = $"Anthropic原生请求处理异常: {ex.Message}";
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

}