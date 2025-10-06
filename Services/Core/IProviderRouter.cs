using OrchestrationApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SqlSugar;
using System.Linq;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 服务商路由结果
/// </summary>
public class ProviderRouteResult
{
    public GroupConfig? Group { get; set; }
    public string? ApiKey { get; set; }
    public string ResolvedModel { get; set; } = string.Empty;
    public Dictionary<string, object> ParameterOverrides { get; set; } = new();
    public bool Success => Group != null && !string.IsNullOrEmpty(ApiKey);
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 失败时的分组ID（用于智能降级）
    /// </summary>
    public string? FailedGroupId { get; set; }
}

/// <summary>
/// 服务商路由接口
/// </summary>
public interface IProviderRouter
{
    /// <summary>
    /// 路由请求到合适的服务商
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="forcedProviderType">强制服务商类型</param>
    /// <param name="excludedGroups">需要排除的分组ID列表（用于避免重复选择已失败的分组）</param>
    /// <returns>路由结果</returns>
    /// </summary>
    Task<ProviderRouteResult> RouteRequestAsync(string model, string proxyKey, string? forcedProviderType, HashSet<string>? excludedGroups = null);

    /// <summary>
    /// 获取指定代理密钥允许访问的分组
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>允许访问的分组</returns>
    /// </summary>
    Task<List<GroupConfig>> GetAllowedGroupsAsync(string proxyKey, string? providerType);

    /// <summary>
    /// 根据模型名称查找合适的分组
    /// <param name="model">模型名称</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>合适的分组</returns>
    /// </summary>
    Task<List<GroupConfig>> FindGroupsByModelAsync(string model, string providerType);

    /// <summary>
    /// 验证分组权限
    /// <param name="groupId">分组ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>是否允许访问</returns>
    /// </summary>
    Task<bool> CheckGroupPermissionAsync(string groupId, string? proxyKey, string? providerType);

    /// <summary>
    /// 解析模型别名
    /// <param name="model">模型名称</param>
    /// <param name="aliases">模型别名</param>
    /// <returns>解析后的模型名称</returns>
    /// </summary>
    string ResolveModelAlias(string model, Dictionary<string, string> aliases);
}

/// <summary>
/// 服务商路由实现
/// </summary>
public class ProviderRouter : IProviderRouter
{
    private readonly ISqlSugarClient _db;
    private readonly IKeyManager _keyManager;
    private readonly ILogger<ProviderRouter> _logger;
    private readonly IMemoryCache _cache;

    public ProviderRouter(
        ISqlSugarClient db,
        IKeyManager keyManager,
        ILogger<ProviderRouter> logger,
        IMemoryCache cache)
    {
        _db = db;
        _keyManager = keyManager;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// 路由请求到合适的服务商
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="forcedProviderType">强制服务商类型</param>
    /// <param name="excludedGroups">需要排除的分组ID列表（用于避免重复选择已失败的分组）</param>
    /// <returns>路由结果</returns>
    public async Task<ProviderRouteResult> RouteRequestAsync(
        string model,
        string proxyKey,
        string? forcedProviderType,
        HashSet<string>? excludedGroups = null)
    {
        try
        {
            _logger.LogDebug("开始路由请求 - 模型: {Model}, 代理密钥: {ProxyKey}, 强制服务商: {ForcedProvider}",
                model, proxyKey?.AsSpan(0, Math.Min(proxyKey.Length, 8)).ToString() + "...", forcedProviderType);

            // 1. 验证代理密钥
            ProxyKey? validatedProxyKey = null;
            if (!string.IsNullOrEmpty(proxyKey))
            {
                validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
                if (validatedProxyKey == null)
                {
                    return new ProviderRouteResult
                    {
                        ErrorMessage = "无效的代理密钥"
                    };
                }

                // 检查RPM限制
                var rpmAllowed = await _keyManager.CheckRpmLimitAsync(validatedProxyKey.Id, "");
                if (!rpmAllowed)
                {
                    return new ProviderRouteResult
                    {
                        ErrorMessage = "超出RPM限制"
                    };
                }
            }

            // 2. 获取候选分组
            var candidateGroups = await GetCandidateGroupsAsync(model, proxyKey, forcedProviderType);
            if (candidateGroups.Count == 0)
            {
                return new ProviderRouteResult
                {
                    ErrorMessage = $"未找到支持模型 {model} 的分组"
                };
            }

            // 2.1 排除已失败的分组
            if (excludedGroups != null && excludedGroups.Count > 0)
            {
                var originalCount = candidateGroups.Count;
                candidateGroups = candidateGroups.Where(g => !excludedGroups.Contains(g.Id)).ToList();
                if (candidateGroups.Count == 0)
                {
                    return new ProviderRouteResult
                    {
                        ErrorMessage = $"所有支持模型 {model} 的分组都已尝试过且失败"
                    };
                }
                _logger.LogDebug("排除已失败的分组 [{ExcludedGroups}] 后，候选分组从 {OriginalCount} 减少到 {RemainingCount}",
                    string.Join(", ", excludedGroups), originalCount, candidateGroups.Count);
            }

            // 3. 应用代理密钥的分组间负载均衡策略
            var selectedGroup = SelectGroupByProxyKeyPolicy(candidateGroups, validatedProxyKey, forcedProviderType);

            // 4. 检查分组权限
            if (!await CheckGroupPermissionAsync(selectedGroup.Id, proxyKey, forcedProviderType))
            {
                _logger.LogDebug("分组 {GroupId} 权限检查失败", selectedGroup.Id);
                return new ProviderRouteResult
                {
                    ErrorMessage = "分组权限检查失败"
                };
            }

            // 5. 检查分组RPM限制
            if (validatedProxyKey != null)
            {
                var groupRpmAllowed = await _keyManager.CheckRpmLimitAsync(validatedProxyKey.Id, selectedGroup.Id);
                if (!groupRpmAllowed)
                {
                    _logger.LogDebug("分组 {GroupId} RPM限制检查失败", selectedGroup.Id);
                    return new ProviderRouteResult
                    {
                        ErrorMessage = "超出分组RPM限制"
                    };
                }
            }

            // 6. 尝试获取可用的API密钥
            var apiKey = await _keyManager.GetNextKeyAsync(selectedGroup.Id);
            if (string.IsNullOrEmpty(apiKey))
            {
                return new ProviderRouteResult
                {
                    ErrorMessage = "选定分组没有可用的API密钥",
                    FailedGroupId = selectedGroup.Id // 记录失败的分组ID用于智能降级
                };
            }

            // 7. 解析模型别名
            var modelAliases = string.IsNullOrEmpty(selectedGroup.ModelAliases) ?
                new Dictionary<string, string>() :
                JsonConvert.DeserializeObject<Dictionary<string, string>>(selectedGroup.ModelAliases) ?? new Dictionary<string, string>();

            var resolvedModel = ResolveModelAlias(model, modelAliases);

            // 8. 获取参数覆盖
            var parameterOverrides = string.IsNullOrEmpty(selectedGroup.ParameterOverrides) ?
                new Dictionary<string, object>() :
                JsonConvert.DeserializeObject<Dictionary<string, object>>(selectedGroup.ParameterOverrides) ?? new Dictionary<string, object>();

            _logger.LogDebug("成功路由到分组 {GroupId} ({GroupName}), 服务商: {ProviderType}",
                selectedGroup.Id, selectedGroup.GroupName, selectedGroup.ProviderType);

            return new ProviderRouteResult
            {
                Group = selectedGroup,
                ApiKey = apiKey,
                ResolvedModel = resolvedModel,
                ParameterOverrides = parameterOverrides
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路由请求时发生异常");
            return new ProviderRouteResult
            {
                ErrorMessage = "路由请求时发生内部错误"
            };
        }
    }

    /// <summary>
    /// 获取指定代理密钥允许访问的分组
    /// </summary>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>允许访问的分组</returns>
    public async Task<List<GroupConfig>> GetAllowedGroupsAsync(string proxyKey, string? providerType)
    {
        try
        {
            var validatedProxyKey = await _keyManager.ValidateProxyKeyAsync(proxyKey);
            if (validatedProxyKey == null)
                return [];

            var allowedGroupIds = string.IsNullOrEmpty(validatedProxyKey.AllowedGroups) ?
                [] :
                JsonConvert.DeserializeObject<List<string>>(validatedProxyKey.AllowedGroups) ?? [];

            if (allowedGroupIds.Count == 0)
            {
                // 如果没有指定允许的分组，则返回所有启用的分组
                return await _db.Queryable<GroupConfig>()
                    .Where(g => !g.IsDeleted && g.Enabled && (string.IsNullOrEmpty(providerType) || g.ProviderType == providerType))
                    .ToListAsync();
            }

            return await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted && allowedGroupIds.Contains(g.Id) && g.Enabled && (string.IsNullOrEmpty(providerType) || g.ProviderType == providerType))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取允许的分组时发生异常");
            return [];
        }
    }

    /// <summary>
    /// 根据模型名称查找合适的分组
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>合适的分组</returns>
    public async Task<List<GroupConfig>> FindGroupsByModelAsync(string model, string? providerType = null)
    {
        try
        {
            var cacheKey = $"groups_by_model:{model}:{providerType ?? "all"}";
            if (_cache.TryGetValue(cacheKey, out List<GroupConfig>? cachedGroups))
            {
                return cachedGroups ?? new List<GroupConfig>();
            }

            var query = _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted && g.Enabled && (string.IsNullOrEmpty(providerType) || g.ProviderType == providerType));

            var allGroups = await query.ToListAsync();

            var matchingGroups = new List<GroupConfig>();

            foreach (var group in allGroups)
            {
                var models = string.IsNullOrEmpty(group.Models) ?
                    new List<string>() :
                    JsonConvert.DeserializeObject<List<string>>(group.Models) ?? new List<string>();

                var modelAliases = string.IsNullOrEmpty(group.ModelAliases) ?
                    new Dictionary<string, string>() :
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();

                // 检查是否直接支持该模型
                if (models.Contains(model))
                {
                    matchingGroups.Add(group);
                    continue;
                }

                // 检查是否通过别名支持该模型
                if (modelAliases.ContainsKey(model))
                {
                    matchingGroups.Add(group);
                    continue;
                }

                // 检查模型是否匹配服务商的通用模式
                //if (IsModelCompatibleWithProvider(model, group.ProviderType))
                //{
                //    matchingGroups.Add(group);
                //}
            }

            _cache.Set(cacheKey, matchingGroups, TimeSpan.FromMinutes(5));
            return matchingGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据模型查找分组时发生异常");
            return new List<GroupConfig>();
        }
    }

    /// <summary>
    /// 验证分组权限
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>是否允许访问</returns>
    public async Task<bool> CheckGroupPermissionAsync(string groupId, string? proxyKey, string? providerType)
    {
        try
        {
            if (string.IsNullOrEmpty(proxyKey))
            {
                // 没有代理密钥，检查是否允许匿名访问
                return true; // 这里可以根据实际需求调整
            }

            var allowedGroups = await GetAllowedGroupsAsync(proxyKey, providerType);
            return allowedGroups.Any(g => g.Id == groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查分组权限时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 解析模型别名
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="aliases">模型别名</param>
    /// <returns>解析后的模型名称</returns>
    public string ResolveModelAlias(string model, Dictionary<string, string> aliases)
    {
        if (aliases.TryGetValue(model, out var aliasModel))
        {
            _logger.LogDebug("模型别名解析: {OriginalModel} -> {ResolvedModel}", model, aliasModel);
            return aliasModel;
        }

        return model;
    }

    /// <summary>
    /// 获取候选分组
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="forcedProviderType">强制服务商类型</param>
    /// <returns>候选分组</returns>
    private async Task<List<GroupConfig>> GetCandidateGroupsAsync(
        string model,
        string? proxyKey,
        string? forcedProviderType)
    {
        var candidateGroups = new List<GroupConfig>();

        // 如果指定了强制服务商类型
        if (!string.IsNullOrEmpty(forcedProviderType))
        {
            // 根据模型与强制服务商类型查找支持的分组
            var modelGroupsWithProvider = await FindGroupsByModelAsync(model, forcedProviderType);
            candidateGroups.AddRange(modelGroupsWithProvider);
        }
        else
        {
            // 根据模型查找支持的分组
            var modelGroups = await FindGroupsByModelAsync(model);
            candidateGroups.AddRange(modelGroups);
        }

        // 如果有代理密钥，进一步过滤允许的分组
        if (!string.IsNullOrEmpty(proxyKey))
        {
            var allowedGroups = await GetAllowedGroupsAsync(proxyKey, forcedProviderType);
            var allowedGroupIds = allowedGroups.Select(g => g.Id).ToHashSet();

            candidateGroups = candidateGroups
                .Where(g => allowedGroupIds.Contains(g.Id))
                .ToList();
        }

        return candidateGroups.Distinct().ToList();
    }

    /// <summary>
    /// 根据代理密钥的分组间负载均衡策略选择分组
    /// </summary>
    /// <param name="candidateGroups">候选分组</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <param name="forcedProviderType">强制服务商类型</param>
    /// <returns>选择的分组</returns>
    private GroupConfig SelectGroupByProxyKeyPolicy(List<GroupConfig> candidateGroups, ProxyKey? proxyKey, string? forcedProviderType)
    {

        List<GroupConfig> filteredGroups = string.IsNullOrEmpty(forcedProviderType)
            ? candidateGroups
            : candidateGroups.Where(g => g.ProviderType == forcedProviderType).ToList();

        // 如果没有代理密钥或只有一个候选分组，使用默认故障转移策略
        if (proxyKey == null || filteredGroups.Count == 1)
        {
            return filteredGroups.OrderByDescending(g => g.Priority).First();
        }

        var policy = proxyKey.GroupBalancePolicy?.ToLower() ?? "failover";
        _logger.LogDebug("应用代理密钥分组间负载均衡策略: {Policy}, 候选分组数: {GroupCount}",
            policy, filteredGroups.Count);

        return policy switch
        {
            "round_robin" => SelectGroupByRoundRobin(filteredGroups, proxyKey.Id),
            "weighted" => SelectGroupByWeight(filteredGroups, proxyKey),
            "random" => SelectGroupByRandom(filteredGroups),
            "failover" => SelectGroupByFailover(filteredGroups),
            _ => SelectGroupByFailover(filteredGroups) // 默认使用故障转移策略
        };
    }

    /// <summary>
    /// 按故障转移策略选择分组（按优先级顺序选择）
    /// </summary>
    /// <param name="groups">分组</param>
    /// <returns>选择的分组</returns>
    private static GroupConfig SelectGroupByFailover(List<GroupConfig> groups)
    {
        return groups.OrderByDescending(g => g.Priority).First();
    }

    /// <summary>
    /// 按轮询策略选择分组
    /// </summary>
    /// <param name="groups">分组</param>
    /// <param name="proxyKeyId">代理密钥ID</param>
    /// <returns>选择的分组</returns>
    private GroupConfig SelectGroupByRoundRobin(List<GroupConfig> groups, int proxyKeyId)
    {
        try
        {
            var cacheKey = $"proxy_rr_index:{proxyKeyId}";
            var currentIndex = 0;

            if (_cache.TryGetValue(cacheKey, out int cachedIndex))
            {
                currentIndex = cachedIndex;
            }

            var selectedIndex = currentIndex % groups.Count;
            var nextIndex = (selectedIndex + 1) % groups.Count;

            _cache.Set(cacheKey, nextIndex, TimeSpan.FromHours(1));

            _logger.LogDebug("轮询选择分组 - 代理密钥: {ProxyKeyId}, 索引: {Index}, 分组: {GroupId}",
                proxyKeyId, selectedIndex, groups.ToList()[selectedIndex].Id);

            return groups.ToList()[selectedIndex];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "轮询选择分组时发生异常，回退到故障转移策略");
            return SelectGroupByFailover(groups);
        }
    }

    /// <summary>
    /// 按随机策略选择分组
    /// </summary>
    /// <param name="groups">分组</param>
    /// <returns>选择的分组</returns>
    private static GroupConfig SelectGroupByRandom(List<GroupConfig> groups)
    {
        var random = new Random();
        var selectedIndex = random.Next(groups.Count);
        return groups.ToList()[selectedIndex];
    }

    /// <summary>
    /// 按权重策略选择分组
    /// </summary>
    /// <param name="groups">分组</param>
    /// <param name="proxyKey">代理密钥</param>
    /// <returns>选择的分组</returns>
    private GroupConfig SelectGroupByWeight(List<GroupConfig> groups, ProxyKey proxyKey)
    {
        try
        {
            // 解析权重配置
            var weights = new Dictionary<string, int>();
            if (!string.IsNullOrEmpty(proxyKey.GroupWeights))
            {
                try
                {
                    var groupWeights = JsonConvert.DeserializeObject<List<GroupWeight>>(proxyKey.GroupWeights);
                    if (groupWeights != null)
                    {
                        weights = groupWeights.ToDictionary(gw => gw.GroupId, gw => gw.Weight);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "解析GroupWeights失败，使用默认权重。GroupWeights内容: {GroupWeights}", proxyKey.GroupWeights);
                    weights = new Dictionary<string, int>();
                }
            }

            // 为没有配置权重的分组设置默认权重1
            var weightedGroups = new List<(GroupConfig Group, int Weight)>();
            foreach (var group in groups)
            {
                var weight = weights.TryGetValue(group.Id, out var w) ? w : 1;
                weightedGroups.Add((group, Math.Max(weight, 0))); // 确保权重非负
            }

            // 计算总权重
            var totalWeight = weightedGroups.Sum(g => g.Weight);
            if (totalWeight == 0)
            {
                // 所有权重都为0，回退到故障转移策略
                return SelectGroupByFailover(groups);
            }

            // 使用加权随机选择
            var random = new Random();
            var randomValue = random.Next(totalWeight);
            var currentWeight = 0;

            foreach (var (group, weight) in weightedGroups)
            {
                currentWeight += weight;
                if (randomValue < currentWeight)
                {
                    _logger.LogDebug("权重选择分组 - 分组: {GroupId}, 权重: {Weight}/{TotalWeight}",
                        group.Id, weight, totalWeight);
                    return group;
                }
            }

            // 理论上不应该到这里，但作为备用
            return weightedGroups.Last().Group;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "权重选择分组时发生异常，回退到故障转移策略");
            return SelectGroupByFailover(groups);
        }
    }

    /// <summary>
    /// 检查模型是否与服务商兼容
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="providerType">服务商类型</param>
    /// <returns>是否兼容</returns>
    private static bool IsModelCompatibleWithProvider(string model, string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => model.StartsWith("gpt-") || model.StartsWith("text-") || model.StartsWith("davinci") || model.StartsWith("o1"),
            "anthropic" => model.StartsWith("claude-"),
            "gemini" => model.StartsWith("gemini-") || model.Contains("gemini"),
            _ => false
        };
    }
}