using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OrchestrationApi.Models;
using OrchestrationApi.Services.Providers;
using OrchestrationApi.Utils;
using SqlSugar;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 健康检查服务实现
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IProxyHttpClientService _httpClientService;
    private readonly IProviderFactory _providerFactory;
    private readonly IConfiguration _configuration;

    public HealthCheckService(
        ISqlSugarClient db,
        ILogger<HealthCheckService> logger,
        IProxyHttpClientService httpClientService,
        IProviderFactory providerFactory,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _httpClientService = httpClientService;
        _providerFactory = providerFactory;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckProviderHealthAsync(string groupId, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var group = await GetGroupConfigAsync(groupId);
            if (group == null)
            {
                return CreateFailedResult(groupId, null, null, null, HealthCheckTypes.Provider,
                    404, 0, "分组不存在", group?.ProviderType, group?.BaseUrl);
            }

            var httpClient = _httpClientService.CreateHttpClient(group, 10); // 10秒超时
            var provider = _providerFactory.GetProvider(group.ProviderType);

            if (provider == null)
            {
                return CreateFailedResult(groupId, null, null, null, HealthCheckTypes.Provider,
                    500, (int)stopwatch.ElapsedMilliseconds, "不支持的服务商类型", group.ProviderType, group.BaseUrl);
            }

            // 构建健康检查请求URL
            var baseUrl = provider.GetBaseUrl(new ProviderConfig { BaseUrl = group.BaseUrl });
            var healthCheckUrl = $"{baseUrl.TrimEnd('/')}/models"; // 使用模型列表端点作为健康检查

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            // 构建HTTP请求，统一使用SendAsync方法
            using var request = new HttpRequestMessage(HttpMethod.Get, healthCheckUrl);

            if (!string.IsNullOrEmpty(apiKey))
            {
                // 如果提供了API密钥，则添加认证头
                var headers = provider.PrepareRequestHeaders(apiKey, new ProviderConfig { BaseUrl = group.BaseUrl });
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await httpClient.SendAsync(request, cts.Token);
            stopwatch.Stop();

            var isSuccess = response.IsSuccessStatusCode;
            var statusCode = (int)response.StatusCode;
            var responseTime = (int)stopwatch.ElapsedMilliseconds;

            // 根据是否使用API密钥生成相应的错误消息
            string? errorMessage = null;
            if (!isSuccess)
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // 使用了API密钥的认证检查
                    errorMessage = statusCode switch
                    {
                        401 => "API密钥无效或未授权",
                        403 => "API密钥权限不足",
                        404 => "服务商端点不存在",
                        429 => "API密钥请求限流",
                        500 => "服务商内部错误",
                        503 => "服务商服务不可用",
                        _ => $"服务商认证检查失败 (HTTP {statusCode})"
                    };
                }
                else
                {
                    // 基础连接检查
                    errorMessage = statusCode switch
                    {
                        404 => "服务商端点不存在",
                        403 => "服务商访问被拒绝",
                        500 => "服务商内部错误",
                        503 => "服务商服务不可用",
                        _ => $"服务商连接失败 (HTTP {statusCode})"
                    };
                }
            }

            _logger.LogDebug("服务商健康检查完成 - GroupId: {GroupId}, StatusCode: {StatusCode}, ResponseTime: {ResponseTime}ms, WithAuth: {WithAuth}",
                groupId, statusCode, responseTime, !string.IsNullOrEmpty(apiKey));

            return CreateHealthCheckResult(groupId, null, null, null, HealthCheckTypes.Provider,
                statusCode, responseTime, isSuccess, errorMessage,
                group.ProviderType, group.BaseUrl);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateFailedResult(groupId, null, null, null, HealthCheckTypes.Provider,
                408, (int)stopwatch.ElapsedMilliseconds, "请求超时", null, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "检查服务商健康状态时发生异常: {GroupId}", groupId);
            return CreateFailedResult(groupId, null, null, null, HealthCheckTypes.Provider,
                500, (int)stopwatch.ElapsedMilliseconds, ex.Message, null, null);
        }
    }

    public async Task<HealthCheckResult> CheckApiKeyHealthAsync(string groupId, string apiKey, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var group = await GetGroupConfigAsync(groupId);
            if (group == null)
            {
                return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                    ApiKeyMaskingUtils.MaskApiKey(apiKey), null, HealthCheckTypes.ApiKey,
                    404, 0, "分组不存在", group?.ProviderType, group?.BaseUrl);
            }

            var httpClient = _httpClientService.CreateHttpClient(group, 10);
            var provider = _providerFactory.GetProvider(group.ProviderType);

            if (provider == null)
            {
                return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                    ApiKeyMaskingUtils.MaskApiKey(apiKey), null, HealthCheckTypes.ApiKey,
                    500, (int)stopwatch.ElapsedMilliseconds, "不支持的服务商类型", group.ProviderType, group.BaseUrl);
            }

            // 构建API密钥验证请求
            var baseUrl = provider.GetBaseUrl(new ProviderConfig { BaseUrl = group.BaseUrl });
            var modelsUrl = $"{baseUrl.TrimEnd('/')}/models";

            var headers = provider.PrepareRequestHeaders(apiKey, new ProviderConfig { BaseUrl = group.BaseUrl });

            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await httpClient.SendAsync(request, cts.Token);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseTime = (int)stopwatch.ElapsedMilliseconds;
            var isSuccess = response.IsSuccessStatusCode;

            string? errorMessage = null;
            if (!isSuccess)
            {
                errorMessage = statusCode switch
                {
                    401 => "API密钥无效或未授权",
                    403 => "API密钥权限不足",
                    429 => "API密钥请求限流",
                    404 => "API密钥验证端点不存在",
                    500 => "服务商内部错误",
                    503 => "服务商服务不可用",
                    _ => $"密钥验证失败 (HTTP {statusCode})"
                };
            }

            _logger.LogDebug("API密钥健康检查完成 - GroupId: {GroupId}, KeyHash: {KeyHash}, StatusCode: {StatusCode}, ResponseTime: {ResponseTime}ms",
                groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey), statusCode, responseTime);

            return CreateHealthCheckResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), null, HealthCheckTypes.ApiKey,
                statusCode, responseTime, isSuccess, errorMessage, group.ProviderType, group.BaseUrl);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), null, HealthCheckTypes.ApiKey,
                408, (int)stopwatch.ElapsedMilliseconds, "请求超时", null, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "检查API密钥健康状态时发生异常: {GroupId}", groupId);
            return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), null, HealthCheckTypes.ApiKey,
                500, (int)stopwatch.ElapsedMilliseconds, ex.Message, null, null);
        }
    }

    public async Task<HealthCheckResult> CheckModelHealthAsync(string groupId, string apiKey, string modelId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var group = await GetGroupConfigAsync(groupId);
            if (group == null)
            {
                return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                    ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                    404, 0, "分组不存在", group?.ProviderType, group?.BaseUrl);
            }

            var httpClient = _httpClientService.CreateHttpClient(group, 15);
            var provider = _providerFactory.GetProvider(group.ProviderType);

            if (provider == null)
            {
                return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                    ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                    500, (int)stopwatch.ElapsedMilliseconds, "不支持的服务商类型", group.ProviderType, group.BaseUrl);
            }

            // 构建模型测试请求
            var testRequest = CreateTestChatRequest(modelId);
            var providerConfig = new ProviderConfig { BaseUrl = group.BaseUrl };

            // 根据不同的Provider类型准备请求内容
            HttpContent content;
            try
            {
                content = await PrepareHealthCheckRequestContentAsync(provider, testRequest, providerConfig, cancellationToken);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning("Provider {ProviderType} 不支持健康检查请求格式: {Error}", group.ProviderType, ex.Message);
                return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                    ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                    500, (int)stopwatch.ElapsedMilliseconds, $"Provider不支持的请求格式: {ex.Message}", group.ProviderType, group.BaseUrl);
            }

            var headers = provider.PrepareRequestHeaders(apiKey, providerConfig);

            var baseUrl = provider.GetBaseUrl(providerConfig);
            var endpoint = provider.GetChatCompletionEndpoint();

            // 对于 Gemini provider，需要替换端点中的模型占位符
            if (provider is GeminiProvider)
            {
                endpoint = endpoint.Replace("{model}", modelId);
                _logger.LogDebug("Gemini 健康检查端点已替换模型占位符: {Endpoint}, 模型: {ModelId}", endpoint, modelId);
            }

            var chatUrl = $"{baseUrl.TrimEnd('/')}{endpoint}";

            using var request = new HttpRequestMessage(HttpMethod.Post, chatUrl)
            {
                Content = content
            };

            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await httpClient.SendAsync(request, cts.Token);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseTime = (int)stopwatch.ElapsedMilliseconds;
            var isSuccess = response.IsSuccessStatusCode;

            string? errorMessage = null;
            if (!isSuccess)
            {
                errorMessage = statusCode switch
                {
                    400 => "模型请求参数错误或格式不支持",
                    401 => "API密钥在模型端点无效",
                    403 => "密钥没有额度",
                    404 => "模型不存在或不可用",
                    429 => "模型请求限流",
                    500 => "模型服务内部错误",
                    503 => "模型服务不可用",
                    _ => $"模型检查失败 (HTTP {statusCode})"
                };

                // 记录详细的模型检查失败信息
                _logger.LogWarning("模型健康检查失败 - GroupId: {GroupId}, Model: {ModelId}, StatusCode: {StatusCode}, Error: {ErrorMessage}",
                    groupId, modelId, statusCode, errorMessage);
            }
            else
            {
                _logger.LogDebug("模型健康检查成功 - GroupId: {GroupId}, Model: {ModelId}, ResponseTime: {ResponseTime}ms",
                    groupId, modelId, responseTime);
            }

            return CreateHealthCheckResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                statusCode, responseTime, isSuccess, errorMessage, group.ProviderType, group.BaseUrl);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                408, (int)stopwatch.ElapsedMilliseconds, "请求超时", null, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "检查模型健康状态时发生异常: {GroupId}, Model: {ModelId}", groupId, modelId);
            return CreateFailedResult(groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey),
                ApiKeyMaskingUtils.MaskApiKey(apiKey), modelId, HealthCheckTypes.Model,
                500, (int)stopwatch.ElapsedMilliseconds, ex.Message, null, null);
        }
    }

    private ChatCompletionRequest CreateTestChatRequest(string modelId)
    {
        return new ChatCompletionRequest
        {
            Model = modelId,
            Messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = "Hello"
                }
            },
            MaxTokens = 1,
            Temperature = 0.1f
        };
    }

    /// <summary>
    /// 根据不同的Provider类型准备健康检查请求内容
    /// </summary>
    private async Task<HttpContent> PrepareHealthCheckRequestContentAsync(
        ILLMProvider provider,
        ChatCompletionRequest testRequest,
        ProviderConfig providerConfig,
        CancellationToken cancellationToken)
    {
        // 根据Provider类型使用不同的方法
        switch (provider)
        {
            case AnthropicProvider anthropicProvider:
                // 将ChatCompletionRequest转换为AnthropicMessageRequest
                var anthropicRequest = ConvertChatRequestToAnthropic(testRequest);
                return await anthropicProvider.PrepareAnthropicRequestContentAsync(anthropicRequest, providerConfig, cancellationToken);

            default:
                // 对于其他Provider（OpenAI、Gemini等），使用标准方法
                return await provider.PrepareRequestContentAsync(testRequest, providerConfig, cancellationToken);
        }
    }

    /// <summary>
    /// 将ChatCompletionRequest转换为AnthropicMessageRequest
    /// </summary>
    private AnthropicMessageRequest ConvertChatRequestToAnthropic(ChatCompletionRequest request)
    {
        var anthropicMessages = new List<AnthropicMessage>();

        foreach (var message in request.Messages)
        {
            var anthropicMessage = new AnthropicMessage
            {
                Role = message.Role,
                Content = new List<AnthropicContent>
                {
                    new AnthropicContent
                    {
                        Type = "text",
                        Text = message.Content?.ToString() ?? ""
                    }
                }
            };
            anthropicMessages.Add(anthropicMessage);
        }

        return new AnthropicMessageRequest
        {
            Model = request.Model,
            MaxTokens = request.MaxTokens ?? 1,
            Messages = anthropicMessages,
            Temperature = request.Temperature,
            Stream = false // 健康检查不使用流式
        };
    }

    private async Task<GroupConfig?> GetGroupConfigAsync(string groupId)
    {
        try
        {
            return await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId && !g.IsDeleted)
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组配置时发生异常: {GroupId}", groupId);
            return null;
        }
    }

    private HealthCheckResult CreateHealthCheckResult(string groupId, string? apiKeyHash, string? apiKeyMasked,
        string? modelId, string checkType, int statusCode, int responseTime, bool isSuccess, string? errorMessage,
        string? providerType, string? baseUrl)
    {
        return new HealthCheckResult
        {
            GroupId = groupId,
            ApiKeyHash = apiKeyHash,
            ApiKeyMasked = apiKeyMasked,
            ModelId = modelId,
            CheckType = checkType,
            StatusCode = statusCode,
            ResponseTimeMs = responseTime,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            CheckedAt = DateTime.Now,
            ProviderType = providerType ?? "unknown",
            BaseUrl = baseUrl
        };
    }

    private HealthCheckResult CreateFailedResult(string groupId, string? apiKeyHash, string? apiKeyMasked,
        string? modelId, string checkType, int statusCode, int responseTime, string errorMessage,
        string? providerType, string? baseUrl)
    {
        return CreateHealthCheckResult(groupId, apiKeyHash, apiKeyMasked, modelId, checkType, statusCode, responseTime,
            false, errorMessage, providerType, baseUrl);
    }

    public async Task<List<HealthCheckResult>> CheckGroupCompleteHealthAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var results = new List<HealthCheckResult>();

        try
        {
            var group = await GetGroupConfigAsync(groupId);
            if (group == null || !group.Enabled)
            {
                _logger.LogWarning("分组不存在或已禁用: {GroupId}", groupId);
                return results;
            }

            // 获取API密钥列表
            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();

            // 1. 检查服务商健康状态（如果有API密钥，使用第一个进行认证检查）
            var firstApiKey = apiKeys.FirstOrDefault();
            var providerResult = await CheckProviderHealthAsync(groupId, firstApiKey, cancellationToken);
            results.Add(providerResult);

            // 如果服务商不健康，跳过后续检查
            if (!providerResult.IsHealthy())
            {
                _logger.LogWarning("服务商不健康，跳过密钥和模型检查: {GroupId} - 错误: {Error}",
                    groupId, providerResult.ErrorMessage);
                return results;
            }

            _logger.LogDebug("服务商健康检查通过，开始检查API密钥: {GroupId}", groupId);

            // 2. 检查所有API密钥
            foreach (var apiKey in apiKeys)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var keyResult = await CheckApiKeyHealthAsync(groupId, apiKey, cancellationToken);
                results.Add(keyResult);

                // 3. 如果密钥有效，检查该密钥下的所有模型
                if (keyResult.IsHealthy())
                {
                    _logger.LogDebug("API密钥健康，开始检查模型: {GroupId}, KeyHash: {KeyHash}",
                        groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey));

                    var models = JsonConvert.DeserializeObject<List<string>>(group.Models) ?? new List<string>();
                    foreach (var model in models)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var modelResult = await CheckModelHealthAsync(groupId, apiKey, model, cancellationToken);
                        results.Add(modelResult);

                        // 记录模型检查结果
                        if (!modelResult.IsHealthy())
                        {
                            _logger.LogWarning("模型检查失败但密钥有效 - GroupId: {GroupId}, Model: {Model}, " +
                                "这可能表明模型端点与密钥验证端点存在差异", groupId, model);
                        }

                        // 在模型检查之间添加30秒延迟，防止触发429限制
                        if (models.IndexOf(model) < models.Count - 1) // 不是最后一个模型
                        {
                            _logger.LogDebug("模型检查完成，等待30秒后检查下一个模型 - GroupId: {GroupId}, Model: {Model}", groupId, model);
                            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("API密钥不健康，跳过模型检查: {GroupId}, KeyHash: {KeyHash}, Error: {Error}",
                        groupId, ApiKeyMaskingUtils.ComputeKeyHash(apiKey), keyResult.ErrorMessage);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行分组完整健康检查时发生异常: {GroupId}", groupId);
            return results;
        }
    }

    public async Task<List<HealthCheckResult>> CheckAllGroupsHealthAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<HealthCheckResult>();

        try
        {
            var groups = await _db.Queryable<GroupConfig>()
                .Where(g => g.Enabled && !g.IsDeleted && g.HealthCheckEnabled)
                .ToListAsync();

            foreach (var group in groups)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var groupResults = await CheckGroupCompleteHealthAsync(group.Id, cancellationToken);
                results.AddRange(groupResults);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行所有分组健康检查时发生异常");
            return results;
        }
    }

    public async Task SaveHealthCheckResultAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Insertable(result).ExecuteCommandAsync();

            // 更新统计信息
            await UpdateHealthCheckStatsAsync(result.GroupId, result.CheckType, result.IsSuccess,
                result.ResponseTimeMs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存健康检查结果时发生异常");
        }
    }

    public async Task SaveHealthCheckResultsAsync(List<HealthCheckResult> results, CancellationToken cancellationToken = default)
    {
        if (results.Count == 0) return;

        try
        {
            await _db.Insertable(results).ExecuteCommandAsync();

            // 批量更新统计信息
            var statsUpdates = results.GroupBy(r => new { r.GroupId, r.CheckType })
                .Select(g => new { g.Key.GroupId, g.Key.CheckType, Results = g.ToList() });

            foreach (var statsUpdate in statsUpdates)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var successCount = statsUpdate.Results.Count(r => r.IsSuccess);
                var avgResponseTime = (int)statsUpdate.Results.Average(r => r.ResponseTimeMs);

                await UpdateHealthCheckStatsAsync(statsUpdate.GroupId, statsUpdate.CheckType,
                    successCount > 0, avgResponseTime, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存健康检查结果时发生异常");
        }
    }

    public async Task UpdateHealthCheckStatsAsync(string groupId, string checkType, bool isSuccess, int responseTimeMs, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _db.Queryable<HealthCheckStats>()
                .Where(s => s.GroupId == groupId && s.CheckType == checkType)
                .FirstAsync();

            if (stats == null)
            {
                // 创建新的统计记录
                stats = new HealthCheckStats
                {
                    GroupId = groupId,
                    CheckType = checkType,
                    TotalChecks = 1,
                    SuccessfulChecks = isSuccess ? 1 : 0,
                    FailedChecks = isSuccess ? 0 : 1,
                    AvgResponseTimeMs = responseTimeMs,
                    LastCheckAt = DateTime.Now,
                    LastSuccessAt = isSuccess ? DateTime.Now : null,
                    LastFailureAt = isSuccess ? null : DateTime.Now,
                    ConsecutiveFailures = isSuccess ? 0 : 1,
                    UpdatedAt = DateTime.Now
                };

                await _db.Insertable(stats).ExecuteCommandAsync();
            }
            else
            {
                // 更新现有统计记录
                stats.TotalChecks++;
                if (isSuccess)
                {
                    stats.SuccessfulChecks++;
                    stats.LastSuccessAt = DateTime.Now;
                    stats.ConsecutiveFailures = 0;
                }
                else
                {
                    stats.FailedChecks++;
                    stats.LastFailureAt = DateTime.Now;
                    stats.ConsecutiveFailures++;
                }

                // 更新平均响应时间
                stats.AvgResponseTimeMs = (stats.AvgResponseTimeMs * (stats.TotalChecks - 1) + responseTimeMs) / stats.TotalChecks;
                stats.LastCheckAt = DateTime.Now;
                stats.UpdatedAt = DateTime.Now;

                await _db.Updateable(stats).ExecuteCommandAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新健康检查统计信息时发生异常: {GroupId}, {CheckType}", groupId, checkType);
        }
    }

    public async Task<List<HealthCheckStats>> GetHealthCheckStatsAsync(string groupId, string? checkType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.Queryable<HealthCheckStats>()
                .Where(s => s.GroupId == groupId);

            if (!string.IsNullOrEmpty(checkType))
            {
                query = query.Where(s => s.CheckType == checkType);
            }

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取健康检查统计信息时发生异常: {GroupId}", groupId);
            return new List<HealthCheckStats>();
        }
    }

    public async Task<List<HealthCheckStats>> GetAllHealthCheckStatsAsync(string? checkType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.Queryable<HealthCheckStats>();

            if (!string.IsNullOrEmpty(checkType))
            {
                query = query.Where(s => s.CheckType == checkType);
            }

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有健康检查统计信息时发生异常");
            return new List<HealthCheckStats>();
        }
    }

    public async Task<List<HealthCheckResult>> GetRecentHealthCheckResultsAsync(string groupId, string? checkType = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.Queryable<HealthCheckResult>()
                .Where(r => r.GroupId == groupId);

            if (!string.IsNullOrEmpty(checkType))
            {
                query = query.Where(r => r.CheckType == checkType);
            }

            return await query.OrderByDescending(r => r.CheckedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近健康检查结果时发生异常: {GroupId}", groupId);
            return new List<HealthCheckResult>();
        }
    }

    public async Task<int> CleanupExpiredHealthCheckRecordsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);

            var deletedCount = await _db.Deleteable<HealthCheckResult>()
                .Where(r => r.CheckedAt < cutoffDate)
                .ExecuteCommandAsync();

            _logger.LogInformation("清理了 {Count} 条过期的健康检查记录，保留天数: {RetentionDays}", deletedCount, retentionDays);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期健康检查记录时发生异常");
            return 0;
        }
    }

    /// <summary>
    /// 分析健康检查结果的一致性，提供详细的状态解释
    /// </summary>
    /// <param name="results">健康检查结果列表</param>
    /// <returns>状态分析结果</returns>
    public HealthCheckAnalysis AnalyzeHealthCheckConsistency(List<HealthCheckResult> results)
    {
        var analysis = new HealthCheckAnalysis();

        var providerResults = results.Where(r => r.CheckType == HealthCheckTypes.Provider).ToList();
        var keyResults = results.Where(r => r.CheckType == HealthCheckTypes.ApiKey).ToList();
        var modelResults = results.Where(r => r.CheckType == HealthCheckTypes.Model).ToList();

        analysis.ProviderHealthy = providerResults.Any() && providerResults.All(r => r.IsHealthy());
        analysis.KeysHealthy = keyResults.Any() && keyResults.All(r => r.IsHealthy());
        analysis.ModelsHealthy = modelResults.Any() && modelResults.All(r => r.IsHealthy());

        // 分析不一致的情况
        if (analysis.ProviderHealthy && analysis.KeysHealthy && !analysis.ModelsHealthy)
        {
            analysis.IsInconsistent = true;
            analysis.InconsistencyReason = "服务商和密钥验证通过，但模型检查失败。" +
                "这通常表明 /models 端点可用但 /chat/completions 端点存在问题，" +
                "可能的原因包括：模型不支持、请求格式不兼容、或聊天端点配置问题。";
        }
        else if (analysis.ProviderHealthy && !analysis.KeysHealthy)
        {
            analysis.InconsistencyReason = "服务商连接正常但密钥验证失败，请检查API密钥是否正确。";
        }
        else if (!analysis.ProviderHealthy)
        {
            analysis.InconsistencyReason = "服务商连接失败，请检查网络连接和服务商地址。";
        }

        analysis.TotalChecks = results.Count;
        analysis.SuccessfulChecks = results.Count(r => r.IsHealthy());
        analysis.FailedChecks = analysis.TotalChecks - analysis.SuccessfulChecks;

        return analysis;
    }
}