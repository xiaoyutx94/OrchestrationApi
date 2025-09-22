using OrchestrationApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;
using OrchestrationApi.Services.Providers;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 密钥管理接口
/// </summary>
public interface IKeyManager
{
    /// <summary>
    /// 获取分组的下一个可用密钥
    /// </summary>
    Task<string?> GetNextKeyAsync(string groupId);

    /// <summary>
    /// 验证代理密钥权限
    /// </summary>
    Task<ProxyKey?> ValidateProxyKeyAsync(string proxyKey);

    /// <summary>
    /// 检查密钥是否可用
    /// </summary>
    Task<bool> IsKeyAvailableAsync(string groupId, string apiKey);

    /// <summary>
    /// 报告密钥错误
    /// </summary>
    Task ReportKeyErrorAsync(string groupId, string apiKey, string errorMessage);

    /// <summary>
    /// 重置密钥错误计数
    /// </summary>
    Task ResetKeyErrorCountAsync(string groupId, string apiKey);

    /// <summary>
    /// 更新代理密钥使用统计
    /// </summary>
    Task UpdateProxyKeyUsageAsync(int proxyKeyId);

    /// <summary>
    /// 检查RPM限制
    /// </summary>
    Task<bool> CheckRpmLimitAsync(int proxyKeyId, string groupId);

    /// <summary>
    /// 获取分组的API密钥列表
    /// </summary>
    Task<List<string>> GetGroupApiKeysAsync(string groupId);

    /// <summary>
    /// 获取密钥验证状态
    /// </summary>
    Task<KeyValidation?> GetKeyValidationAsync(string groupId, string apiKeyHash);

    /// <summary>
    /// 获取所有分组
    /// </summary>
    Task<List<GroupConfig>> GetAllGroupsAsync();

    /// <summary>
    /// 获取分组管理数据
    /// </summary>
    Task<object> GetGroupsManageDataAsync();

    /// <summary>
    /// 创建分组
    /// </summary>
    Task<GroupConfig> CreateGroupAsync(GroupRequest groupRequest);

    /// <summary>
    /// 更新分组
    /// </summary>
    Task UpdateGroupAsync(string id, GroupRequest groupRequest);

    /// <summary>
    /// 删除分组
    /// </summary>
    Task DeleteGroupAsync(string id);

    /// <summary>
    /// 切换分组启用状态
    /// </summary>
    Task ToggleGroupAsync(string id);

    /// <summary>
    /// 获取系统状态
    /// </summary>
    Task<SystemStatusResponse> GetSystemStatusAsync();

    /// <summary>
    /// 获取所有API密钥
    /// </summary>
    Task<List<ApiKeyInfo>> GetAllKeysAsync();

    /// <summary>
    /// 添加API密钥
    /// </summary>
    Task<string> AddKeyAsync(string key, string? name, string? description);

    /// <summary>
    /// 批量添加API密钥
    /// </summary>
    Task<BatchAddKeysResult> BatchAddKeysAsync(List<string> keys);

    /// <summary>
    /// 更新API密钥
    /// </summary>
    Task UpdateKeyAsync(string keyId, string key, string? name, string? description);

    /// <summary>
    /// 删除API密钥
    /// </summary>
    Task DeleteKeyAsync(string keyId);

    /// <summary>
    /// 获取密钥状态统计
    /// </summary>
    Task<Dictionary<string, object>> GetKeysStatusAsync();

    /// <summary>
    /// 获取密钥统计信息
    /// </summary>
    Task<KeyStatistics> GetKeysStatisticsAsync();

    /// <summary>
    /// 获取代理密钥列表
    /// </summary>
    Task<List<ProxyKeyInfo>> GetProxyKeysAsync();

    /// <summary>
    /// 生成代理密钥
    /// </summary>
    Task<ProxyKeyInfo> GenerateProxyKeyAsync(string name, string? description);

    /// <summary>
    /// 更新代理密钥
    /// </summary>
    Task UpdateProxyKeyAsync(int keyId, UpdateProxyKeyRequest request);

    /// <summary>
    /// 删除代理密钥
    /// </summary>
    Task DeleteProxyKeyAsync(int keyId);

    /// <summary>
    /// 检查服务商健康状态
    /// </summary>
    Task<object> CheckProviderHealthAsync(string groupId);

    /// <summary>
    /// 获取模型列表
    /// </summary>
    Task<Dictionary<string, object>> GetModelsAsync(string? provider = null);

    /// <summary>
    /// 根据服务商类型获取可用模型
    /// </summary>
    Task<List<object>> GetAvailableModelsByTypeAsync(
        string providerType,
        string? baseUrl,
        List<string> apiKeys,
        int timeoutSeconds,
        int maxRetries,
        Dictionary<string, string> headers);

    /// <summary>
    /// 验证分组密钥
    /// </summary>
    Task<object> ValidateGroupKeysAsync(string groupId, List<string> apiKeys);

    /// <summary>
    /// 获取分组密钥验证状态
    /// </summary>
    Task<object> GetGroupKeyValidationStatusAsync(string groupId);

    /// <summary>
    /// 导出分组配置
    /// </summary>
    Task<string> ExportGroupsAsync(List<string> groupIds);

    /// <summary>
    /// 导入分组配置
    /// </summary>
    Task<ImportGroupsResult> ImportGroupsAsync(string configContent);

    /// <summary>
    /// 刷新健康状态
    /// </summary>
    Task<object> RefreshHealthStatusAsync();

    /// <summary>
    /// 强制更新特定密钥的状态
    /// </summary>
    Task<object> ForceUpdateKeyStatusAsync(string groupId, string apiKey, string status);

    /// <summary>
    /// 获取分组的密钥使用统计
    /// </summary>
    Task<object> GetGroupKeyUsageStatsAsync(string groupId);

    /// <summary>
    /// 更新密钥使用统计（数据库版本）
    /// </summary>
    Task UpdateKeyUsageStatsAsync(string groupId, string apiKey);

    /// <summary>
    /// 获取密钥使用统计（数据库版本）
    /// </summary>
    Task<KeyUsageStats?> GetKeyUsageStatsAsync(string groupId, string apiKeyHash);

    /// <summary>
    /// 检查和恢复无效密钥（后台服务使用）
    /// 返回 Dictionary<string, object> 格式的结果
    /// </summary>
    Task<object> CheckAndRecoverInvalidKeysAsync();

    /// <summary>
    /// 清除所有状态码为401的无效密钥
    /// 从orch_groups、orch_key_validation、orch_key_usage_stats表中删除相关数据
    /// </summary>
    Task<object> ClearInvalidKeysAsync();

    /// <summary>
    /// 清除空白密钥的服务商分组（标记删除）
    /// </summary>
    Task<object> ClearEmptyGroupsAsync();
}

/// <summary>
/// 密钥管理实现
/// </summary>
public class KeyManager : IKeyManager
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<KeyManager> _logger;
    private readonly IMemoryCache _cache;
    private readonly IProviderFactory _providerFactory;
    private readonly Dictionary<string, Dictionary<string, DateTime>> _keyLastUsed;
    private readonly Dictionary<string, Dictionary<string, int>> _keyUsageCount; // 添加密钥使用次数统计
    private readonly Dictionary<string, int> _keyIndexes;
    private readonly Lock _lockObj = new();

    public KeyManager(ISqlSugarClient db, ILogger<KeyManager> logger, IMemoryCache cache, IProviderFactory providerFactory)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
        _providerFactory = providerFactory;
        _keyLastUsed = [];
        _keyUsageCount = []; // 初始化密钥使用次数统计
        _keyIndexes = [];
    }

    public async Task<string?> GetNextKeyAsync(string groupId)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId && g.Enabled && !g.IsDeleted)
                .FirstAsync();

            if (group == null)
            {
                _logger.LogWarning("分组不存在或已禁用: {GroupId}", groupId);
                return null;
            }

            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? [];
            if (apiKeys.Count == 0)
            {
                _logger.LogWarning("分组 {GroupId} 没有配置API密钥", groupId);
                return null;
            }

            // 过滤可用的密钥
            var availableKeys = new List<string>();
            foreach (var key in apiKeys)
            {
                if (await IsKeyAvailableAsync(groupId, key))
                {
                    availableKeys.Add(key);
                }
            }

            if (availableKeys.Count == 0)
            {
                _logger.LogWarning("分组 {GroupId} 没有可用的API密钥", groupId);
                return null;
            }

            // 根据策略选择密钥
            var selectedKey = await SelectKeyByPolicyAsync(groupId, availableKeys, group.BalancePolicy);

            _logger.LogDebug("为分组 {GroupId} 选择密钥，策略: {Policy}", groupId, group.BalancePolicy);
            return selectedKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组 {GroupId} 的下一个密钥时发生异常", groupId);
            return null;
        }
    }

    public async Task<ProxyKey?> ValidateProxyKeyAsync(string proxyKey)
    {
        try
        {
            var cacheKey = $"proxy_key:{proxyKey}";
            if (_cache.TryGetValue(cacheKey, out ProxyKey? cachedKey))
            {
                return cachedKey;
            }

            var key = await _db.Queryable<ProxyKey>()
                .Where(pk => pk.KeyValue == proxyKey && pk.Enabled)
                .FirstAsync();

            if (key != null)
            {
                _cache.Set(cacheKey, key, TimeSpan.FromMinutes(5));
            }

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证代理密钥时发生异常");
            return null;
        }
    }

    public async Task<bool> IsKeyAvailableAsync(string groupId, string apiKey)
    {
        try
        {
            if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("IsKeyAvailableAsync: 无效的参数 - GroupId: {GroupId}, ApiKey: {ApiKey}",
                    groupId, string.IsNullOrEmpty(apiKey) ? "null/empty" : "provided");
                return false;
            }

            var keyHash = ComputeKeyHash(apiKey);
            var validation = await GetKeyValidationAsync(groupId, keyHash);

            if (validation == null)
            {
                // 首次使用，默认认为可用，但记录日志以便追踪
                _logger.LogDebug("密钥未验证，默认认为可用 - GroupId: {GroupId}, KeyHash: {KeyHash}",
                    groupId, keyHash.Substring(0, 8));
                return true;
            }

            // 检查验证记录的时效性（超过24小时的记录可能过时）
            var validationAge = DateTime.Now - validation.LastValidatedAt;
            if (validationAge > TimeSpan.FromHours(24))
            {
                _logger.LogDebug("密钥验证记录过时 - GroupId: {GroupId}, Age: {Age}小时",
                    groupId, validationAge.TotalHours);
                // 过时的记录，谨慎处理，倾向于认为可用但需要重新验证
                return validation.IsValid || validation.ErrorCount < 3;
            }

            // 如果错误次数超过阈值（例如5次），则认为不可用
            if (validation.ErrorCount >= 5)
            {
                // 检查是否应该重新尝试（例如1小时后）
                var shouldRetry = DateTime.Now - validation.LastValidatedAt > TimeSpan.FromHours(1);
                if (shouldRetry)
                {
                    _logger.LogDebug("密钥错误次数过多但允许重试 - GroupId: {GroupId}, ErrorCount: {ErrorCount}",
                        groupId, validation.ErrorCount);
                }
                return shouldRetry;
            }

            // 如果最近有401错误，更谨慎地判断
            if (validation.LastStatusCode == 401 && validationAge < TimeSpan.FromMinutes(30))
            {
                _logger.LogDebug("密钥最近有401错误 - GroupId: {GroupId}", groupId);
                return false;
            }

            return validation.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查密钥可用性时发生异常 - GroupId: {GroupId}", groupId);
            // 发生异常时，为了系统稳定性，默认认为可用
            return true;
        }
    }

    public async Task ReportKeyErrorAsync(string groupId, string apiKey, string errorMessage)
    {
        try
        {
            var keyHash = ComputeKeyHash(apiKey);
            var validation = await GetKeyValidationAsync(groupId, keyHash);

            if (validation == null)
            {
                validation = new KeyValidation
                {
                    GroupId = groupId,
                    ApiKeyHash = keyHash,
                    ProviderType = await GetProviderTypeByGroupIdAsync(groupId),
                    IsValid = false,
                    ErrorCount = 1,
                    LastError = errorMessage,
                    LastStatusCode = null, // 错误报告时可能无状态码
                    LastValidatedAt = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                await _db.Insertable(validation).ExecuteCommandAsync();
            }
            else
            {
                validation.IsValid = false;
                validation.ErrorCount++;
                validation.LastError = errorMessage;
                validation.LastStatusCode = null; // 错误报告时可能无状态码
                validation.LastValidatedAt = DateTime.Now;

                await _db.Updateable(validation).ExecuteCommandAsync();
            }

            _logger.LogWarning("报告密钥错误 - 分组: {GroupId}, 错误: {Error}", groupId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报告密钥错误时发生异常");
        }
    }

    public async Task ResetKeyErrorCountAsync(string groupId, string apiKey)
    {
        try
        {
            var keyHash = ComputeKeyHash(apiKey);
            var validation = await GetKeyValidationAsync(groupId, keyHash);

            if (validation != null)
            {
                validation.IsValid = true;
                validation.ErrorCount = 0;
                validation.LastError = "";
                validation.LastValidatedAt = DateTime.Now;

                await _db.Updateable(validation).ExecuteCommandAsync();
                _logger.LogDebug("重置密钥错误计数 - 分组: {GroupId}", groupId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置密钥错误计数时发生异常");
        }
    }

    public async Task UpdateProxyKeyUsageAsync(int proxyKeyId)
    {
        try
        {
            await _db.Updateable<ProxyKey>()
                .SetColumns(pk => new ProxyKey
                {
                    LastUsedAt = DateTime.Now,
                    UsageCount = pk.UsageCount + 1
                })
                .Where(pk => pk.Id == proxyKeyId)
                .ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新代理密钥使用统计时发生异常");
        }
    }

    public async Task<bool> CheckRpmLimitAsync(int proxyKeyId, string groupId)
    {
        try
        {
            // 获取代理密钥和分组的RPM限制
            var proxyKey = await _db.Queryable<ProxyKey>()
                .Where(pk => pk.Id == proxyKeyId)
                .FirstAsync();

            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (proxyKey == null || group == null)
                return true; // 如果找不到，默认允许

            var rpmLimit = Math.Min(
                proxyKey.RpmLimit > 0 ? proxyKey.RpmLimit : int.MaxValue,
                group.RpmLimit > 0 ? group.RpmLimit : int.MaxValue
            );

            if (rpmLimit == int.MaxValue)
                return true; // 没有限制

            // 检查过去一分钟的请求数
            var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
            var requestCount = await _db.Queryable<RequestLog>()
                .Where(rl => rl.ProxyKeyId == proxyKeyId && rl.CreatedAt >= oneMinuteAgo)
                .CountAsync();

            return requestCount < rpmLimit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查RPM限制时发生异常");
            return true; // 默认允许
        }
    }

    public async Task<List<string>> GetGroupApiKeysAsync(string groupId)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
                return [];

            return JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组API密钥时发生异常");
            return [];
        }
    }

    public async Task<KeyValidation?> GetKeyValidationAsync(string groupId, string apiKeyHash)
    {
        try
        {
            return await _db.Queryable<KeyValidation>()
                .Where(kv => kv.GroupId == groupId && kv.ApiKeyHash == apiKeyHash)
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取密钥验证状态时发生异常");
            return null;
        }
    }

    private string SelectRoundRobinKey(string groupId, List<string> keys)
    {
        if (!_keyIndexes.TryGetValue(groupId, out int value))
        {
            value = 0;
            _keyIndexes[groupId] = value;
        }

        var index = value % keys.Count;
        _keyIndexes[groupId] = (index + 1) % keys.Count;

        return keys[index];
    }

    private static string SelectRandomKey(List<string> keys)
    {
        var random = new Random();
        var index = random.Next(keys.Count);
        return keys[index];
    }

    /// <summary>
    /// 选择使用次数最少的密钥（基于数据库）
    /// </summary>
    private async Task<string> SelectLeastUsedKeyAsync(string groupId, List<string> keys)
    {
        try
        {
            var keyUsageList = new List<(string Key, long UsageCount)>();

            // 获取所有密钥的使用统计
            foreach (var key in keys)
            {
                var keyHash = ComputeKeyHash(key);
                var stats = await GetKeyUsageStatsAsync(groupId, keyHash);
                var usageCount = stats?.UsageCount ?? 0;
                keyUsageList.Add((key, usageCount));
            }

            // 找到使用次数最少的密钥
            var leastUsedKey = keyUsageList.OrderBy(x => x.UsageCount).First().Key;

            // 更新选中密钥的使用统计
            await UpdateKeyUsageStatsAsync(groupId, leastUsedKey);

            var finalStats = await GetKeyUsageStatsAsync(groupId, ComputeKeyHash(leastUsedKey));
            _logger.LogDebug("选择最少使用密钥 - 分组: {GroupId}, 密钥: {KeyPrefix}, 使用次数: {UsageCount}",
                groupId, leastUsedKey.Substring(0, Math.Min(8, leastUsedKey.Length)), finalStats?.UsageCount ?? 0);

            return leastUsedKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择最少使用密钥时发生异常，回退到第一个可用密钥");
            return keys.First();
        }
    }

    /// <summary>
    /// 选择使用次数最少的密钥（内存版本，保留作为备用）
    /// </summary>
    private string SelectLeastUsedKeyMemory(string groupId, List<string> keys)
    {
        lock (_lockObj)
        {
            // 确保分组的使用次数统计字典存在
            if (!_keyUsageCount.TryGetValue(groupId, out Dictionary<string, int>? usageCount))
            {
                usageCount = [];
                _keyUsageCount[groupId] = usageCount;
            }

            // 为新密钥初始化使用次数为0
            foreach (var key in keys)
            {
                if (!usageCount.ContainsKey(key))
                {
                    usageCount[key] = 0;
                }
            }

            // 找到使用次数最少的密钥
            var leastUsedKey = keys.OrderBy(k => usageCount[k]).First();

            // 增加选中密钥的使用次数
            usageCount[leastUsedKey]++;

            // 同时更新最后使用时间（用于其他统计）
            if (!_keyLastUsed.TryGetValue(groupId, out Dictionary<string, DateTime>? lastUsed))
            {
                lastUsed = [];
                _keyLastUsed[groupId] = lastUsed;
            }
            lastUsed[leastUsedKey] = DateTime.Now;

            _logger.LogDebug("选择最少使用密钥（内存） - 分组: {GroupId}, 密钥: {KeyPrefix}, 使用次数: {UsageCount}",
                groupId, leastUsedKey.Substring(0, Math.Min(8, leastUsedKey.Length)), usageCount[leastUsedKey]);

            return leastUsedKey;
        }
    }

    /// <summary>
    /// 解析允许的分组列表
    /// </summary>
    private static List<string> ParseAllowedGroups(string? allowedGroupsJson)
    {
        if (string.IsNullOrEmpty(allowedGroupsJson))
            return new List<string>();

        try
        {
            var groups = JsonConvert.DeserializeObject<List<string>>(allowedGroupsJson);
            return groups ?? new List<string>();
        }
        catch
        {
            // 如果解析失败，返回空列表
            return new List<string>();
        }
    }

    /// <summary>
    /// 解析分组权重列表
    /// </summary>
    private static List<GroupWeight>? ParseGroupWeights(string? groupWeightsJson)
    {
        if (string.IsNullOrEmpty(groupWeightsJson))
            return new List<GroupWeight>();

        try
        {
            var weights = JsonConvert.DeserializeObject<List<GroupWeight>>(groupWeightsJson);
            return weights ?? new List<GroupWeight>();
        }
        catch
        {
            // 如果解析失败，返回空列表
            return new List<GroupWeight>();
        }
    }

    private static string ComputeKeyHash(string apiKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<List<GroupConfig>> GetAllGroupsAsync()
    {
        try
        {
            return await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted)
                .OrderBy(g => g.Id)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有分组时发生异常");
            return [];
        }
    }

    public async Task<object> GetGroupsManageDataAsync()
    {
        try
        {
            var groups = await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted)
                .OrderBy(g => g.Id)
                .ToListAsync();

            var groupsDict = new Dictionary<string, object>();

            foreach (var group in groups)
            {
                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                var models = JsonConvert.DeserializeObject<List<string>>(group.Models) ?? new List<string>();
                var modelAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.ModelAliases) ?? new Dictionary<string, string>();
                var parameterOverrides = JsonConvert.DeserializeObject<Dictionary<string, object>>(group.ParameterOverrides) ?? new Dictionary<string, object>();
                var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers) ?? new Dictionary<string, string>();

                // 改进的密钥状态检查逻辑
                var availableKeysCount = 0;
                var hasAvailableKey = false;
                var validatedKeysCount = 0; // 已验证的密钥数量

                if (group.Enabled && apiKeys.Any())
                {
                    foreach (var apiKey in apiKeys)
                    {
                        var keyHash = ComputeKeyHash(apiKey);
                        var validation = await GetKeyValidationAsync(group.Id, keyHash);

                        if (validation != null)
                        {
                            validatedKeysCount++;
                            // 使用验证记录判断可用性
                            if (validation.IsValid && validation.ErrorCount < 5)
                            {
                                availableKeysCount++;
                                hasAvailableKey = true;
                            }
                            else if (validation.ErrorCount >= 5)
                            {
                                // 检查是否应该重新尝试（1小时后）
                                var shouldRetry = DateTime.Now - validation.LastValidatedAt > TimeSpan.FromHours(1);
                                if (shouldRetry)
                                {
                                    availableKeysCount++;
                                    hasAvailableKey = true;
                                }
                            }
                        }
                        else
                        {
                            // 未验证的密钥，为避免显示0，暂时认为可用，但标记需要验证
                            // 这样可以避免首次加载时显示"0 0"的问题
                            availableKeysCount++;
                            hasAvailableKey = true;
                        }
                    }
                }

                // 如果所有密钥都未验证，提供合理的默认值
                if (apiKeys.Any() && validatedKeysCount == 0)
                {
                    // 所有密钥都未验证，显示总数但标记为需要验证
                    availableKeysCount = apiKeys.Count;
                    hasAvailableKey = group.Enabled;
                }

                var isHealthy = group.Enabled && hasAvailableKey;

                // 数据验证和一致性检查
                var totalKeys = Math.Max(0, apiKeys.Count);
                var availableKeys = Math.Max(0, Math.Min(availableKeysCount, totalKeys)); // 确保可用密钥数不超过总数

                // 记录数据验证信息
                if (availableKeys != availableKeysCount)
                {
                    _logger.LogWarning("分组 {GroupId} 密钥数据不一致，已修正 - 原始可用数: {Original}, 修正后: {Corrected}, 总数: {Total}",
                        group.Id, availableKeysCount, availableKeys, totalKeys);
                }

                // 如果分组禁用但显示有可用密钥，修正数据
                if (!group.Enabled && availableKeys > 0)
                {
                    _logger.LogDebug("分组 {GroupId} 已禁用，将可用密钥数重置为0", group.Id);
                    availableKeys = 0;
                    hasAvailableKey = false;
                    isHealthy = false;
                }

                groupsDict[group.Id.ToString()] = new
                {
                    id = group.Id,
                    group_name = group.GroupName,
                    provider_type = group.ProviderType,
                    base_url = group.BaseUrl,
                    api_keys = apiKeys,
                    models = models,
                    model_aliases = modelAliases,
                    parameter_overrides = parameterOverrides,
                    headers = headers,
                    balance_policy = group.BalancePolicy,
                    retry_count = group.RetryCount,
                    timeout = group.Timeout,
                    rpm_limit = group.RpmLimit,
                    test_model = group.TestModel,
                    priority = group.Priority,
                    enabled = group.Enabled,
                    healthy = isHealthy,
                    total_keys = totalKeys,
                    available_keys = availableKeys,
                    validation_status = validatedKeysCount > 0 ? "verified" : "unverified", // 添加验证状态标识
                    verified_keys_count = validatedKeysCount, // 已验证的密钥数量
                    created_at = group.CreatedAt,
                    updated_at = group.UpdatedAt
                };
            }

            var result = new
            {
                success = true,
                groups = groupsDict
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组管理数据时发生异常");
            throw;
        }
    }

    public async Task<GroupConfig> CreateGroupAsync(GroupRequest groupRequest)
    {
        try
        {
            var groupConfig = new GroupConfig
            {
                Id = !string.IsNullOrEmpty(groupRequest.Id) ? groupRequest.Id : GenerateGroupId(groupRequest.GroupName),
                GroupName = groupRequest.GroupName,
                ProviderType = groupRequest.ProviderType,
                BaseUrl = groupRequest.BaseUrl,
                ApiKeys = JsonConvert.SerializeObject(groupRequest.ApiKeys),
                Models = JsonConvert.SerializeObject(groupRequest.Models),
                ModelAliases = JsonConvert.SerializeObject(groupRequest.ModelAliases),
                ParameterOverrides = JsonConvert.SerializeObject(groupRequest.ParameterOverrides),
                Headers = JsonConvert.SerializeObject(groupRequest.Headers),
                BalancePolicy = groupRequest.BalancePolicy,
                RetryCount = groupRequest.RetryCount,
                Timeout = groupRequest.Timeout,
                RpmLimit = groupRequest.RpmLimit,
                TestModel = groupRequest.TestModel ?? string.Empty,
                Priority = groupRequest.Priority,
                Enabled = groupRequest.Enabled,
                FakeStreaming = groupRequest.FakeStreaming, // 添加假流配置支持
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var result = await _db.Insertable(groupConfig).ExecuteReturnEntityAsync();
            _logger.LogInformation("创建分组成功: {GroupName} (ID: {GroupId})", groupRequest.GroupName, result.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建分组时发生异常");
            throw;
        }
    }

    public async Task UpdateGroupAsync(string id, GroupRequest groupRequest)
    {
        try
        {
            var existingGroup = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == id)
                .FirstAsync();

            if (existingGroup == null)
            {
                throw new InvalidOperationException($"分组 {id} 不存在");
            }

            existingGroup.GroupName = groupRequest.GroupName;
            existingGroup.ProviderType = groupRequest.ProviderType;
            existingGroup.BaseUrl = groupRequest.BaseUrl;
            existingGroup.ApiKeys = JsonConvert.SerializeObject(groupRequest.ApiKeys);
            existingGroup.Models = JsonConvert.SerializeObject(groupRequest.Models);
            existingGroup.ModelAliases = JsonConvert.SerializeObject(groupRequest.ModelAliases);
            existingGroup.ParameterOverrides = JsonConvert.SerializeObject(groupRequest.ParameterOverrides);
            existingGroup.Headers = JsonConvert.SerializeObject(groupRequest.Headers);
            existingGroup.BalancePolicy = groupRequest.BalancePolicy;
            existingGroup.RetryCount = groupRequest.RetryCount;
            existingGroup.Timeout = groupRequest.Timeout;
            existingGroup.RpmLimit = groupRequest.RpmLimit;
            existingGroup.TestModel = groupRequest.TestModel ?? string.Empty;
            existingGroup.Priority = groupRequest.Priority;
            existingGroup.Enabled = groupRequest.Enabled;
            existingGroup.FakeStreaming = groupRequest.FakeStreaming; // 添加假流配置支持
            existingGroup.UpdatedAt = DateTime.Now;

            await _db.Updateable(existingGroup).ExecuteCommandAsync();
            _logger.LogInformation("更新分组成功: {GroupName} (ID: {GroupId})", groupRequest.GroupName, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分组时发生异常");
            throw;
        }
    }

    public async Task DeleteGroupAsync(string id)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == id && !g.IsDeleted)
                .FirstAsync();

            if (group == null)
            {
                throw new InvalidOperationException($"分组 {id} 不存在");
            }

            await _db.Deleteable<GroupConfig>()
                .Where(g => g.Id == id)
                .ExecuteCommandAsync();

            _logger.LogInformation("删除分组成功: {GroupName} (ID: {GroupId})", group.GroupName, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除分组时发生异常");
            throw;
        }
    }

    public async Task ToggleGroupAsync(string id)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == id && !g.IsDeleted)
                .FirstAsync();

            if (group == null)
            {
                throw new InvalidOperationException($"分组 {id} 不存在");
            }

            group.Enabled = !group.Enabled;
            group.UpdatedAt = DateTime.Now;

            await _db.Updateable(group).ExecuteCommandAsync();
            _logger.LogInformation("切换分组状态成功: {GroupName} (ID: {GroupId}) - {Status}",
                group.GroupName, id, group.Enabled ? "启用" : "禁用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换分组状态时发生异常");
            throw;
        }
    }

    public async Task<SystemStatusResponse> GetSystemStatusAsync()
    {
        try
        {
            var groups = await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted)
                .ToListAsync();

            var groupStatuses = new List<GroupStatus>();

            foreach (var group in groups)
            {
                var totalKeys = 0;
                var validKeys = 0;

                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                totalKeys = apiKeys.Count;

                // 检查有效密钥数量
                foreach (var apiKey in apiKeys)
                {
                    if (await IsKeyAvailableAsync(group.Id, apiKey))
                    {
                        validKeys++;
                    }
                }

                // 获取请求统计
                var requestsCount = await _db.Queryable<RequestLog>()
                    .Where(rl => rl.GroupId == group.Id)
                    .CountAsync();

                var lastRequest = await _db.Queryable<RequestLog>()
                    .Where(rl => rl.GroupId == group.Id)
                    .OrderByDescending(rl => rl.CreatedAt)
                    .FirstAsync();

                groupStatuses.Add(new GroupStatus
                {
                    Id = group.Id,
                    GroupName = group.GroupName,
                    ProviderType = group.ProviderType,
                    Enabled = group.Enabled,
                    TotalKeys = totalKeys,
                    ValidKeys = validKeys,
                    RequestsCount = requestsCount,
                    LastRequestAt = lastRequest?.CreatedAt
                });
            }

            // 获取总请求统计
            var totalRequests = await _db.Queryable<RequestLog>().CountAsync();
            var successfulRequests = await _db.Queryable<RequestLog>()
                .Where(rl => rl.StatusCode >= 200 && rl.StatusCode < 300)
                .CountAsync();
            var failedRequests = totalRequests - successfulRequests;

            return new SystemStatusResponse
            {
                Status = "running",
                Version = "1.0.0",
                Uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime,
                Groups = groupStatuses,
                TotalRequests = totalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统状态时发生异常");
            throw;
        }
    }

    public async Task<List<ApiKeyInfo>> GetAllKeysAsync()
    {
        try
        {
            // 这里需要一个新的表来存储独立的API密钥信息
            // 暂时返回一个模拟的列表，实际应该从数据库读取
            var keys = new List<ApiKeyInfo>();

            // 从所有分组中提取密钥信息
            var groups = await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted)
                .ToListAsync();
            foreach (var group in groups)
            {
                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                foreach (var apiKey in apiKeys)
                {
                    var keyHash = ComputeKeyHash(apiKey);
                    var validation = await GetKeyValidationAsync(group.Id, keyHash);

                    keys.Add(new ApiKeyInfo
                    {
                        Id = keyHash,
                        Key = apiKey,
                        Name = $"分组 {group.GroupName} 密钥",
                        Description = $"来自分组: {group.GroupName}",
                        IsActive = validation?.IsValid ?? true,
                        UsageCount = 0, // 需要从请求日志中统计
                        ErrorCount = validation?.ErrorCount ?? 0,
                        LastUsed = validation?.LastValidatedAt,
                        LastError = validation?.LastError,
                        CreatedAt = group.CreatedAt
                    });
                }
            }

            return keys.DistinctBy(k => k.Key).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有API密钥时发生异常");
            return new List<ApiKeyInfo>();
        }
    }

    public async Task<string> AddKeyAsync(string key, string? name, string? description)
    {
        try
        {
            // 这里需要实现添加独立密钥的逻辑
            // 暂时返回密钥哈希作为ID
            var keyHash = ComputeKeyHash(key);
            _logger.LogInformation("添加API密钥: {Name}", name);
            await Task.CompletedTask;
            return keyHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加API密钥时发生异常");
            throw;
        }
    }

    public async Task<BatchAddKeysResult> BatchAddKeysAsync(List<string> keys)
    {
        try
        {
            var result = new BatchAddKeysResult();
            var errors = new List<string>();

            foreach (var key in keys)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        errors.Add("密钥不能为空");
                        continue;
                    }

                    await AddKeyAsync(key, null, null);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"添加密钥失败: {ex.Message}");
                }
            }

            result.Errors = errors;
            result.SkippedCount = keys.Count - result.SuccessCount;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量添加API密钥时发生异常");
            throw;
        }
    }

    public async Task UpdateKeyAsync(string keyId, string key, string? name, string? description)
    {
        try
        {
            // 这里需要实现更新密钥的逻辑
            _logger.LogInformation("更新API密钥: {KeyId}", keyId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新API密钥时发生异常");
            throw;
        }
    }

    public async Task DeleteKeyAsync(string keyId)
    {
        try
        {
            // 这里需要实现删除密钥的逻辑
            _logger.LogInformation("删除API密钥: {KeyId}", keyId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除API密钥时发生异常");
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetKeysStatusAsync()
    {
        try
        {
            var groups = await _db.Queryable<GroupConfig>().ToListAsync();
            var result = new Dictionary<string, object>();

            foreach (var group in groups)
            {
                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                var validKeys = 0;
                var invalidKeys = 0;

                foreach (var apiKey in apiKeys)
                {
                    if (await IsKeyAvailableAsync(group.Id, apiKey))
                        validKeys++;
                    else
                        invalidKeys++;
                }

                result[group.Id.ToString()] = new
                {
                    total_keys = apiKeys.Count,
                    valid_keys = validKeys,
                    invalid_keys = invalidKeys,
                    last_validated = DateTime.Now
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取密钥状态统计时发生异常");
            return new Dictionary<string, object>();
        }
    }

    public async Task<KeyStatistics> GetKeysStatisticsAsync()
    {
        try
        {
            var groups = await _db.Queryable<GroupConfig>().ToListAsync();
            var totalKeys = 0;
            var activeKeys = 0;

            foreach (var group in groups)
            {
                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                totalKeys += apiKeys.Count;

                foreach (var apiKey in apiKeys)
                {
                    if (await IsKeyAvailableAsync(group.Id, apiKey))
                        activeKeys++;
                }
            }

            var totalRequests = await _db.Queryable<RequestLog>().CountAsync();
            var totalErrors = await _db.Queryable<RequestLog>()
                .Where(rl => rl.StatusCode >= 400)
                .CountAsync();

            return new KeyStatistics
            {
                TotalKeys = totalKeys,
                ActiveKeys = activeKeys,
                TotalRequests = totalRequests,
                TotalErrors = totalErrors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取密钥统计信息时发生异常");
            throw;
        }
    }

    public async Task<List<ProxyKeyInfo>> GetProxyKeysAsync()
    {
        try
        {
            var proxyKeys = await _db.Queryable<ProxyKey>()
                .Where(pk => pk.Enabled)
                .ToListAsync();

            return proxyKeys.Select(pk => new ProxyKeyInfo
            {
                Id = pk.Id,
                Key = pk.KeyValue,
                Name = pk.KeyName,
                Description = pk.Description,
                UsageCount = (int)pk.UsageCount,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.Enabled,
                AllowedGroups = ParseAllowedGroups(pk.AllowedGroups),
                GroupBalancePolicy = pk.GroupBalancePolicy,
                RpmLimit = pk.RpmLimit,
                GroupSelectionConfig = new GroupSelectionConfig
                {
                    Strategy = pk.GroupBalancePolicy,
                    GroupWeights = ParseGroupWeights(pk.GroupWeights)
                }
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取代理密钥列表时发生异常");
            return new List<ProxyKeyInfo>();
        }
    }

    public async Task<ProxyKeyInfo> GenerateProxyKeyAsync(string name, string? description)
    {
        try
        {
            // 生成类似OpenAI API密钥格式的密钥
            // OpenAI密钥格式: sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
            // 使用Base64编码的随机字节，然后转换为URL安全的Base64字符串
            var randomBytes = new byte[32]; // 32字节 = 256位
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // 转换为Base64并移除填充字符，然后替换URL不安全的字符
            var base64String = Convert.ToBase64String(randomBytes)
                .Replace("+", "A")
                .Replace("/", "B")
                .Replace("=", "");

            // 确保长度为48个字符（sk- + 44个字符）
            var keyValue = "sk-" + base64String.Substring(0, Math.Min(44, base64String.Length));

            // 验证生成的密钥格式
            _logger.LogDebug("生成代理密钥: {KeyName}, 格式: {KeyFormat}", name, keyValue);

            var proxyKey = new ProxyKey
            {
                KeyValue = keyValue,
                KeyName = name,
                Description = description,
                CreatedAt = DateTime.Now,
                LastUsedAt = null,
                UsageCount = 0,
                RpmLimit = 0,
                Enabled = true
            };

            var result = await _db.Insertable(proxyKey).ExecuteReturnEntityAsync();

            return new ProxyKeyInfo
            {
                Id = result.Id,
                Key = result.KeyValue,
                Name = result.KeyName,
                Description = result.Description,
                UsageCount = (int)result.UsageCount,
                CreatedAt = result.CreatedAt,
                IsActive = result.Enabled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成代理密钥时发生异常");
            throw;
        }
    }

    public async Task UpdateProxyKeyAsync(int keyId, UpdateProxyKeyRequest request)
    {
        try
        {
            // 获取现有的代理密钥
            var existingKey = await _db.Queryable<ProxyKey>()
                .Where(pk => pk.Id == keyId)
                .FirstAsync();

            if (existingKey == null)
            {
                throw new ArgumentException($"代理密钥不存在: {keyId}");
            }

            // 更新代理密钥字段
            existingKey.KeyName = request.Name;
            existingKey.Description = request.Description;
            existingKey.Enabled = request.IsActive;

            // 处理 AllowedGroups - 转换为 JSON 字符串
            if (request.AllowedGroups.Any())
            {
                existingKey.AllowedGroups = JsonConvert.SerializeObject(request.AllowedGroups);
            }
            else
            {
                existingKey.AllowedGroups = "[]";
            }

            // 处理分组选择配置
            if (request.GroupSelectionConfig != null)
            {
                existingKey.GroupBalancePolicy = request.GroupSelectionConfig.Strategy;

                // 处理分组权重
                if (request.GroupSelectionConfig.GroupWeights != null && request.GroupSelectionConfig.GroupWeights.Any())
                {
                    existingKey.GroupWeights = JsonConvert.SerializeObject(request.GroupSelectionConfig.GroupWeights);
                }
                else
                {
                    existingKey.GroupWeights = "[]";
                }
            }
            else
            {
                // 如果没有分组选择配置，回退到旧的字段处理方式
                if (!string.IsNullOrEmpty(request.GroupBalancePolicy))
                {
                    existingKey.GroupBalancePolicy = request.GroupBalancePolicy;
                }
            }

            if (request.RpmLimit.HasValue)
            {
                existingKey.RpmLimit = request.RpmLimit.Value;
            }

            // 执行更新
            await _db.Updateable(existingKey).ExecuteCommandAsync();

            _logger.LogInformation("更新代理密钥成功: {KeyId}, 名称: {KeyName}, 状态: {IsActive}",
                keyId, request.Name, request.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新代理密钥时发生异常: {KeyId}", keyId);
            throw;
        }
    }

    public async Task DeleteProxyKeyAsync(int keyId)
    {
        try
        {
            await _db.Deleteable<ProxyKey>()
                .Where(pk => pk.Id == keyId)
                .ExecuteCommandAsync();

            _logger.LogInformation("删除代理密钥成功: {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除代理密钥时发生异常");
            throw;
        }
    }

    public async Task<object> CheckProviderHealthAsync(string groupId)
    {
        try
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return new { healthy = false, error = "无效的分组ID" };
            }

            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
            {
                return new { healthy = false, error = "分组不存在" };
            }

            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
            var availableKeys = 0;

            foreach (var apiKey in apiKeys)
            {
                if (await IsKeyAvailableAsync(groupId, apiKey))
                    availableKeys++;
            }

            return new
            {
                healthy = availableKeys > 0,
                total_keys = apiKeys.Count,
                available_keys = availableKeys,
                group_name = group.GroupName,
                provider_type = group.ProviderType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查服务商健康状态时发生异常");
            return new { healthy = false, error = ex.Message };
        }
    }

    public async Task<List<object>> GetAvailableModelsByTypeAsync(
        string providerType,
        string? baseUrl,
        List<string> apiKeys,
        int timeoutSeconds,
        int maxRetries,
        Dictionary<string, string> headers)
    {
        try
        {
            _logger.LogInformation("获取 {ProviderType} 的可用模型列表", providerType);

            if (!apiKeys.Any())
            {
                _logger.LogWarning("未提供API密钥，无法获取模型列表");
                return new List<object>();
            }

            // 获取服务商实例
            ILLMProvider provider;
            try
            {
                provider = _providerFactory.GetProvider(providerType);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "不支持的服务商类型: {ProviderType}", providerType);
                return new List<object>();
            }

            // 直接传递所有密钥给Provider，让Provider内部处理密钥重试逻辑
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            var providerConfig = new ProviderConfig
            {
                ApiKeys = apiKeys, // 传递所有密钥，由Provider内部遍历尝试
                BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl,
                TimeoutSeconds = timeoutSeconds,
                MaxRetries = maxRetries,
                // headers 参数目前未在 ProviderConfig 中使用，此处保留
            };

            var modelsResponse = await provider.GetModelsAsync(providerConfig, cts.Token);

            if (modelsResponse?.Data != null && modelsResponse.Data.Any())
            {
                var models = modelsResponse.Data.Select(m => new
                {
                    id = m.Id,
                    name = m.Id,
                    description = $"Model from {providerType}",
                    owned_by = m.OwnedBy,
                    created = m.Created
                }).ToList();

                _logger.LogDebug("成功从 {ProviderType} 获取 {ModelCount} 个模型", providerType, models.Count);
                return models.Cast<object>().ToList();
            }
            else
            {
                _logger.LogWarning("从 {ProviderType} 获取的模型列表为空", providerType);
                // 不返回默认模型，直接返回空列表
                return new List<object>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 {ProviderType} 可用模型时发生异常", providerType);

            // 发生异常时也不返回默认模型列表，抛出异常让上层处理
            throw new InvalidOperationException($"无法从 {providerType} 获取模型列表: {ex.Message}", ex);
        }
    }

    private List<object> GetDefaultModels(string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => new List<object>
            {
                new { id = "gpt-4o", name = "GPT-4o", description = "最新的GPT-4模型", owned_by = "openai", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-4o-mini", name = "GPT-4o Mini", description = "轻量版GPT-4模型", owned_by = "openai", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-4-turbo", name = "GPT-4 Turbo", description = "GPT-4 Turbo模型", owned_by = "openai", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-4", name = "GPT-4", description = "标准GPT-4模型", owned_by = "openai", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-3.5-turbo", name = "GPT-3.5 Turbo", description = "GPT-3.5 Turbo模型", owned_by = "openai", created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            },
            "anthropic" => new List<object>
            {
                new { id = "claude-3-5-sonnet-20241022", name = "Claude 3.5 Sonnet", description = "最新的Claude 3.5 Sonnet模型", owned_by = "anthropic", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "claude-3-opus-20240229", name = "Claude 3 Opus", description = "Claude 3 Opus模型", owned_by = "anthropic", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "claude-3-sonnet-20240229", name = "Claude 3 Sonnet", description = "Claude 3 Sonnet模型", owned_by = "anthropic", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "claude-3-haiku-20240307", name = "Claude 3 Haiku", description = "Claude 3 Haiku模型", owned_by = "anthropic", created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            },
            "gemini" => new List<object>
            {
                new { id = "gemini-1.5-pro", name = "Gemini 1.5 Pro", description = "Gemini 1.5 Pro模型", owned_by = "google", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gemini-1.5-flash", name = "Gemini 1.5 Flash", description = "Gemini 1.5 Flash模型", owned_by = "google", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gemini-pro", name = "Gemini Pro", description = "Gemini Pro模型", owned_by = "google", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gemini-pro-vision", name = "Gemini Pro Vision", description = "支持视觉的Gemini Pro模型", owned_by = "google", created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            },
            "azure" => new List<object>
            {
                new { id = "gpt-4o", name = "Azure GPT-4o", description = "Azure版GPT-4o模型", owned_by = "microsoft", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-4", name = "Azure GPT-4", description = "Azure版GPT-4模型", owned_by = "microsoft", created = DateTimeOffset.Now.ToUnixTimeSeconds() },
                new { id = "gpt-35-turbo", name = "Azure GPT-3.5 Turbo", description = "Azure版GPT-3.5 Turbo模型", owned_by = "microsoft", created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            },
            _ => new List<object>
            {
                new { id = "default-model", name = "Default Model", description = "默认模型", owned_by = "unknown", created = DateTimeOffset.Now.ToUnixTimeSeconds() }
            }
        };
    }

    public async Task<Dictionary<string, object>> GetModelsAsync(string? provider = null)
    {
        try
        {
            // 返回模拟的模型列表，实际应该根据服务商类型返回相应的模型
            var models = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(provider) || provider == "openai")
            {
                models["openai"] = new[]
                {
                    new { id = "gpt-4", name = "GPT-4" },
                    new { id = "gpt-4-turbo", name = "GPT-4 Turbo" },
                    new { id = "gpt-3.5-turbo", name = "GPT-3.5 Turbo" }
                };
            }

            if (string.IsNullOrEmpty(provider) || provider == "anthropic")
            {
                models["anthropic"] = new[]
                {
                    new { id = "claude-3-opus-20240229", name = "Claude 3 Opus" },
                    new { id = "claude-3-sonnet-20240229", name = "Claude 3 Sonnet" },
                    new { id = "claude-3-haiku-20240307", name = "Claude 3 Haiku" }
                };
            }

            if (string.IsNullOrEmpty(provider) || provider == "gemini")
            {
                models["gemini"] = new[]
                {
                    new { id = "gemini-pro", name = "Gemini Pro" },
                    new { id = "gemini-pro-vision", name = "Gemini Pro Vision" }
                };
            }

            await Task.CompletedTask;
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模型列表时发生异常");
            return new Dictionary<string, object>();
        }
    }

    public async Task<object> ValidateGroupKeysAsync(string groupId, List<string> apiKeys)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
            {
                return new { success = false, error = "分组不存在" };
            }

            var validKeys = 0;
            var invalidKeys = 0;
            var totalKeys = apiKeys.Count;

            // 获取对应的服务商实例
            var provider = _providerFactory.GetProvider(group.ProviderType);

            foreach (var apiKey in apiKeys)
            {
                try
                {
                    // 使用实际的API调用验证密钥
                    var validationResult = await ValidateApiKeyWithProviderAsync(provider, apiKey, group);

                    var keyHash = ComputeKeyHash(apiKey);
                    var validation = await GetKeyValidationAsync(groupId, keyHash);

                    if (validation == null)
                    {
                        validation = new KeyValidation
                        {
                            GroupId = groupId,
                            ApiKeyHash = keyHash,
                            ProviderType = group.ProviderType,
                            IsValid = validationResult.IsValid,
                            ErrorCount = validationResult.IsValid ? 0 : 1,
                            LastError = validationResult.IsValid ? "" : (validationResult.ErrorMessage.Length > 0 ? validationResult.ErrorMessage : "密钥无效或无法访问"),
                            LastStatusCode = validationResult.StatusCode,
                            LastValidatedAt = DateTime.Now,
                            CreatedAt = DateTime.Now
                        };

                        await _db.Insertable(validation).ExecuteCommandAsync();
                    }
                    else
                    {
                        validation.IsValid = validationResult.IsValid;
                        validation.ErrorCount = validationResult.IsValid ? 0 : validation.ErrorCount + 1;
                        validation.LastError = validationResult.IsValid ? "" : (validationResult.ErrorMessage.Length > 0 ? validationResult.ErrorMessage : "密钥验证失败");
                        validation.LastStatusCode = validationResult.StatusCode;
                        validation.LastValidatedAt = DateTime.Now;

                        await _db.Updateable(validation).ExecuteCommandAsync();
                    }

                    if (validationResult.IsValid)
                        validKeys++;
                    else
                        invalidKeys++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "验证密钥时发生异常: {ApiKey}", apiKey.Substring(0, Math.Min(8, apiKey.Length)));
                    invalidKeys++;

                    // 记录验证异常到数据库
                    var keyHash = ComputeKeyHash(apiKey);
                    var validation = await GetKeyValidationAsync(groupId, keyHash);

                    if (validation == null)
                    {
                        validation = new KeyValidation
                        {
                            GroupId = groupId,
                            ApiKeyHash = keyHash,
                            ProviderType = group.ProviderType,
                            IsValid = false,
                            ErrorCount = 1,
                            LastError = ex.Message,
                            LastStatusCode = null, // 异常情况下无状态码
                            LastValidatedAt = DateTime.Now,
                            CreatedAt = DateTime.Now
                        };

                        await _db.Insertable(validation).ExecuteCommandAsync();
                    }
                    else
                    {
                        validation.IsValid = false;
                        validation.ErrorCount = validation.ErrorCount + 1;
                        validation.LastError = ex.Message;
                        validation.LastStatusCode = null; // 异常情况下无状态码
                        validation.LastValidatedAt = DateTime.Now;

                        await _db.Updateable(validation).ExecuteCommandAsync();
                    }
                }
            }

            return new
            {
                success = true,
                valid_keys = validKeys,
                invalid_keys = invalidKeys,
                total_keys = totalKeys
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证分组密钥时发生异常");
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// 密钥验证结果
    /// </summary>
    private class KeyValidationResult
    {
        public bool IsValid { get; set; }
        public int? StatusCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 使用对应的服务商进行实际的API密钥验证
    /// </summary>
    private async Task<KeyValidationResult> ValidateApiKeyWithProviderAsync(ILLMProvider provider, string apiKey, GroupConfig group)
    {
        try
        {
            // 对于 Gemini 服务商，使用专门的验证方法
            if (provider.ProviderType.ToLower() == "gemini")
            {
                return await ValidateGeminiApiKeyAsync(provider, apiKey, group);
            }

            // 对于其他服务商，使用通用的 OpenAI 格式验证
            return await ValidateGenericApiKeyAsync(provider, apiKey, group);
        }
        catch (Exception ex)
        {
            // 记录详细的验证失败原因
            _logger.LogDebug(ex, "API密钥验证失败 - Provider: {ProviderType}, Error: {Error}",
                provider.ProviderType, ex.Message);
            return new KeyValidationResult { IsValid = false, StatusCode = null, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 使用通用 OpenAI 格式进行API密钥验证
    /// </summary>
    private async Task<KeyValidationResult> ValidateGenericApiKeyAsync(ILLMProvider provider, string apiKey, GroupConfig group)
    {
        try
        {
            // 构建简单的验证请求配置
            var groupHeaders = string.IsNullOrEmpty(group.Headers)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers) ?? new Dictionary<string, string>();

            var providerConfig = new ProviderConfig
            {
                ApiKeys = [apiKey],
                BaseUrl = group.BaseUrl,
                TimeoutSeconds = 30,
                MaxRetries = 0,  // 验证时不重试
                GroupId = group.Id,
                GroupName = group.GroupName,
                Headers = groupHeaders  // 添加分组配置的请求头
            };

            // 创建一个非常简单的测试请求，限制最大token数量
            var configuredModels = JsonConvert.DeserializeObject<string[]>(group.Models);
            var testModel = string.IsNullOrWhiteSpace(group.TestModel)
                ? (configuredModels?.FirstOrDefault() ?? GetDefaultModelForProvider(group.ProviderType))
                : group.TestModel;

            var testRequest = new ChatCompletionRequest
            {
                Model = testModel,
                Messages =
                [
                    new ChatMessage { Role = "user", Content = "hi" }
                ],
                MaxTokens = 1,  // 限制响应长度
                Temperature = 0  // 确保响应一致性
            };

            // 使用30秒超时进行验证
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // 调用实际的API使用新的HTTP代理方法
            var httpContent = await provider.PrepareRequestContentAsync(testRequest, providerConfig, cts.Token);
            var httpResponse = await provider.SendHttpRequestAsync(httpContent, apiKey, providerConfig, false, cts.Token);

            // 返回验证结果和状态码
            return new KeyValidationResult
            {
                IsValid = httpResponse.IsSuccess,
                StatusCode = httpResponse.StatusCode,
                ErrorMessage = httpResponse.ErrorMessage ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "通用API密钥验证失败 - Provider: {ProviderType}, Error: {Error}",
                provider.ProviderType, ex.Message);
            return new KeyValidationResult { IsValid = false, StatusCode = null, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 使用 Gemini 原生格式进行API密钥验证
    /// </summary>
    private async Task<KeyValidationResult> ValidateGeminiApiKeyAsync(ILLMProvider provider, string apiKey, GroupConfig group)
    {
        try
        {
            // 构建 Gemini 专用的验证请求配置
            var groupHeaders = string.IsNullOrEmpty(group.Headers)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(group.Headers) ?? new Dictionary<string, string>();

            var configuredModels = JsonConvert.DeserializeObject<string[]>(group.Models);
            var testModel = string.IsNullOrWhiteSpace(group.TestModel)
                ? (configuredModels?.FirstOrDefault() ?? GetDefaultModelForProvider(group.ProviderType))
                : group.TestModel;

            var providerConfig = new ProviderConfig
            {
                ApiKeys = [apiKey],
                BaseUrl = group.BaseUrl,
                TimeoutSeconds = 30,
                MaxRetries = 0,  // 验证时不重试
                Model = testModel,
                GroupId = group.Id,
                GroupName = group.GroupName,
                Headers = groupHeaders  // 添加分组配置的请求头
            };

            // 创建 Gemini 原生格式的测试请求
            var geminiTestRequest = new GeminiGenerateContentRequest
            {
                Contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = "Hi" }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    MaxOutputTokens = 1,  // 限制响应长度
                    Temperature = 0.0f,    // 确保响应一致性
                    ThinkingConfig = new GeminiThinkingConfig()
                    {
                        ThinkingBudget = 0
                    }
                }
            };

            // 使用30秒超时进行验证
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // 检查 provider 是否为 GeminiProvider 并支持原生请求格式
            if (provider is GeminiProvider geminiProvider)
            {
                // 使用 Gemini 原生请求格式
                var httpContent = await geminiProvider.PrepareRequestContentAsync(geminiTestRequest, providerConfig, cts.Token);
                var httpResponse = await geminiProvider.SendHttpRequestAsync(httpContent, apiKey, providerConfig, false, cts.Token);

                return new KeyValidationResult
                {
                    IsValid = httpResponse.IsSuccess,
                    StatusCode = httpResponse.StatusCode,
                    ErrorMessage = httpResponse.ErrorMessage ?? string.Empty
                };
            }
            else
            {
                return new KeyValidationResult { IsValid = false, StatusCode = null, ErrorMessage = "不支持的Gemini Provider类型" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Gemini API密钥验证失败 - Error: {Error}", ex.Message);
            return new KeyValidationResult { IsValid = false, StatusCode = null, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 根据服务商类型获取默认测试模型
    /// </summary>
    private string GetDefaultModelForProvider(string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => "gpt-3.5-turbo",
            "anthropic" => "claude-3-haiku-20240307",
            "gemini" => "gemini-1.5-flash",
            _ => "gpt-3.5-turbo"
        };
    }

    public async Task<object> GetGroupKeyValidationStatusAsync(string groupId)
    {
        try
        {
            // 获取分组信息
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
            {
                return new { success = false, error = "分组不存在" };
            }

            // 获取验证记录
            var validations = await _db.Queryable<KeyValidation>()
                .Where(kv => kv.GroupId == groupId)
                .ToListAsync();

            var validationStatus = new Dictionary<string, object>();

            foreach (var validation in validations)
            {
                // 使用简化的标识符代替完整哈希
                var keyId = validation.ApiKeyHash.Substring(0, 8);
                validationStatus[keyId] = new
                {
                    is_valid = validation.IsValid,
                    error_count = validation.ErrorCount,
                    last_error = validation.LastError,
                    last_validated_at = validation.LastValidatedAt
                };
            }

            // 计算密钥统计信息
            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
            var totalKeys = apiKeys.Count;
            var validKeys = 0;
            var invalidKeys = 0;
            var unknownKeys = 0; // 新增：未检测的密钥数量

            // 统计有效、无效和未检测密钥数量
            foreach (var apiKey in apiKeys)
            {
                var keyHash = ComputeKeyHash(apiKey);
                var validation = validations.FirstOrDefault(v => v.ApiKeyHash == keyHash);

                if (validation != null)
                {
                    // 有验证记录的密钥
                    if (validation.IsValid && validation.ErrorCount < 5)
                    {
                        validKeys++;
                    }
                    else
                    {
                        invalidKeys++;
                    }
                }
                else
                {
                    // 没有验证记录的密钥，标记为未检测
                    unknownKeys++;
                }
            }

            return new
            {
                success = true,
                validation_status = validationStatus,
                // 添加密钥统计信息，包含未检测状态
                total_keys = totalKeys,
                valid_keys = validKeys,
                invalid_keys = invalidKeys,
                unknown_keys = unknownKeys, // 新增：未检测的密钥数量
                last_validated = validations.Any() ? 
                    validations.Max(v => v.LastValidatedAt) : (DateTime?)null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组密钥验证状态时发生异常");
            return new { success = false, error = ex.Message };
        }
    }

    public async Task<string> ExportGroupsAsync(List<string> groupIds)
    {
        try
        {
            var groups = await _db.Queryable<GroupConfig>()
                .Where(g => groupIds.Contains(g.Id.ToString()))
                .ToListAsync();

            if (!groups.Any())
            {
                throw new InvalidOperationException("未找到指定的分组");
            }

            var exportData = new
            {
                export_info = new
                {
                    export_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    total_groups = groups.Count,
                    source = "OrchestrationApi"
                },
                groups = groups.Select(g => new
                {
                    group_name = g.GroupName,
                    provider_type = g.ProviderType,
                    api_keys = JsonConvert.DeserializeObject<List<string>>(g.ApiKeys),
                    models = JsonConvert.DeserializeObject<List<string>>(g.Models),
                    model_aliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(g.ModelAliases),
                    parameter_overrides = JsonConvert.DeserializeObject<Dictionary<string, object>>(g.ParameterOverrides),
                    balance_policy = g.BalancePolicy,
                    retry_count = g.RetryCount,
                    timeout = g.Timeout,
                    rpm_limit = g.RpmLimit,
                    priority = g.Priority,
                    enabled = g.Enabled
                }).ToList()
            };

            return JsonConvert.SerializeObject(exportData, Formatting.Indented);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出分组配置时发生异常");
            throw;
        }
    }

    public async Task<ImportGroupsResult> ImportGroupsAsync(string configContent)
    {
        try
        {
            var result = new ImportGroupsResult();
            var errors = new List<string>();

            dynamic? config = JsonConvert.DeserializeObject(configContent);

            if (config?.groups == null)
            {
                throw new InvalidOperationException("配置文件格式无效");
            }

            var groups = config.groups;
            result.TotalGroups = groups.Count;

            foreach (var groupData in groups)
            {
                try
                {
                    var groupRequest = new GroupRequest
                    {
                        GroupName = groupData.group_name,
                        ProviderType = groupData.provider_type,
                        ApiKeys = groupData.api_keys?.ToObject<List<string>>() ?? new List<string>(),
                        Models = groupData.models?.ToObject<List<string>>() ?? new List<string>(),
                        ModelAliases = groupData.model_aliases?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>(),
                        ParameterOverrides = groupData.parameter_overrides?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                        BalancePolicy = groupData.balance_policy ?? "round_robin",
                        RetryCount = (int)(groupData.retry_count ?? 3),
                        Timeout = (int)(groupData.timeout ?? 60),
                        RpmLimit = (int)(groupData.rpm_limit ?? 0),
                        Priority = (int)(groupData.priority ?? 0),
                        Enabled = groupData.enabled ?? true
                    };

                    // 检查是否已存在同名分组
                    var existingGroup = await _db.Queryable<GroupConfig>()
                        .Where(g => g.GroupName == groupRequest.GroupName)
                        .FirstAsync();

                    if (existingGroup != null)
                    {
                        errors.Add($"分组 '{groupRequest.GroupName}' 已存在，跳过导入");
                        continue;
                    }

                    await CreateGroupAsync(groupRequest);
                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"导入分组失败: {ex.Message}");
                }
            }

            result.Errors = errors;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入分组配置时发生异常");
            throw;
        }
    }

    public async Task<object> RefreshHealthStatusAsync()
    {
        try
        {
            _logger.LogInformation("开始刷新所有分组的健康状态");

            var groups = await _db.Queryable<GroupConfig>().ToListAsync();
            var refreshResults = new List<object>();

            foreach (var group in groups)
            {
                var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? [];
                var validKeys = 0;
                var invalidKeys = 0;

                // 获取对应的服务商实例
                var provider = _providerFactory.GetProvider(group.ProviderType);

                foreach (var apiKey in apiKeys)
                {
                    var keyHash = ComputeKeyHash(apiKey);
                    var validation = await GetKeyValidationAsync(group.Id, keyHash);

                    // 使用真实的API调用重新验证密钥
                    var validationResult = await ValidateApiKeyWithProviderAsync(provider, apiKey, group);

                    if (validation == null)
                    {
                        validation = new KeyValidation
                        {
                            GroupId = group.Id,
                            ApiKeyHash = keyHash,
                            ProviderType = group.ProviderType,
                            IsValid = validationResult.IsValid,
                            ErrorCount = validationResult.IsValid ? 0 : 1,
                            LastError = validationResult.IsValid ? "" : (validationResult.ErrorMessage.Length > 0 ? validationResult.ErrorMessage : "密钥验证失败"),
                            LastStatusCode = validationResult.StatusCode,
                            LastValidatedAt = DateTime.Now,
                            CreatedAt = DateTime.Now
                        };
                        await _db.Insertable(validation).ExecuteCommandAsync();
                        _logger.LogDebug("为分组 {GroupName} 创建了新的密钥验证记录 - 状态: {IsValid}",
                            group.GroupName, validationResult.IsValid);
                    }
                    else
                    {
                        var oldStatus = validation.IsValid;
                        validation.IsValid = validationResult.IsValid;
                        validation.ErrorCount = validationResult.IsValid ? 0 : validation.ErrorCount + 1;
                        validation.LastError = validationResult.IsValid ? "" : (validationResult.ErrorMessage.Length > 0 ? validationResult.ErrorMessage : "密钥验证失败");
                        validation.LastStatusCode = validationResult.StatusCode;
                        validation.LastValidatedAt = DateTime.Now;

                        if (validationResult.IsValid && !oldStatus)
                        {
                            _logger.LogInformation("密钥状态恢复正常 - 分组: {GroupName}, 状态: {OldStatus} -> {NewStatus}",
                                group.GroupName, oldStatus, validationResult.IsValid);
                        }

                        await _db.Updateable(validation).ExecuteCommandAsync();
                    }

                    if (validationResult.IsValid) validKeys++; else invalidKeys++;
                }

                refreshResults.Add(new
                {
                    group_id = group.Id,
                    group_name = group.GroupName,
                    provider_type = group.ProviderType,
                    total_keys = apiKeys.Count,
                    valid_keys = validKeys,
                    invalid_keys = invalidKeys
                });

                _logger.LogDebug("完成分组 {GroupName} 的健康检查 - 总密钥: {Total}, 有效: {Valid}, 无效: {Invalid}",
                    group.GroupName, apiKeys.Count, validKeys, invalidKeys);
            }

            var totalKeysChecked = refreshResults.Sum(r => ((dynamic)r).total_keys);
            _logger.LogInformation("健康状态刷新完成 - 检查了 {GroupCount} 个分组，总共 {TotalKeys} 个密钥",
                refreshResults.Count, totalKeysChecked);

            return new
            {
                refreshed_groups = refreshResults.Count,
                total_keys_checked = totalKeysChecked,
                refresh_time = DateTime.Now,
                groups = refreshResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新健康状态时发生异常");
            throw;
        }
    }

    /// <summary>
    /// 根据策略选择密钥（异步版本）
    /// </summary>
    private async Task<string> SelectKeyByPolicyAsync(string groupId, List<string> availableKeys, string balancePolicy)
    {
        if (availableKeys.Count == 0)
            return string.Empty;

        return balancePolicy.ToLower() switch
        {
            "round_robin" => SelectKeyRoundRobin(groupId, availableKeys),
            "random" => availableKeys[new Random().Next(availableKeys.Count)],
            "least_used" => await SelectLeastUsedKeyAsync(groupId, availableKeys), // 使用异步版本
            _ => availableKeys.First() // 默认策略：选择第一个可用密钥
        };
    }

    /// <summary>
    /// 轮询选择密钥
    /// </summary>
    private string SelectKeyRoundRobin(string groupId, List<string> availableKeys)
    {
        lock (_lockObj)
        {
            if (!_keyIndexes.TryGetValue(groupId, out int value))
            {
                value = 0;
                _keyIndexes[groupId] = value;
            }

            var index = value % availableKeys.Count;
            _keyIndexes[groupId] = (index + 1) % availableKeys.Count;

            return availableKeys[index];
        }
    }

    /// <summary>
    /// 根据分组ID获取服务商类型
    /// </summary>
    private async Task<string> GetProviderTypeByGroupIdAsync(string groupId)
    {
        try
        {
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            return group?.ProviderType ?? "openai";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组服务商类型时发生异常: {GroupId}", groupId);
            return "openai"; // 默认返回openai
        }
    }

    /// <summary>
    /// 生成分组ID
    /// </summary>
    private string GenerateGroupId(string groupName)
    {
        // 将分组名称转换为小写，并替换空格和特殊字符为连字符
        var id = groupName.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-");

        // 移除不允许的字符，只保留字母、数字和连字符
        id = System.Text.RegularExpressions.Regex.Replace(id, @"[^a-z0-9\-]", "");

        // 移除连续的连字符
        id = System.Text.RegularExpressions.Regex.Replace(id, @"-+", "-");

        // 移除开头和结尾的连字符
        id = id.Trim('-');

        // 如果结果为空，使用默认前缀
        if (string.IsNullOrEmpty(id))
        {
            id = "group";
        }

        // 添加时间戳确保唯一性
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        return $"{id}-{timestamp}";
    }

    /// <summary>
    /// 强制更新特定密钥的状态
    /// </summary>
    public async Task<object> ForceUpdateKeyStatusAsync(string groupId, string apiKey, string status)
    {
        try
        {
            _logger.LogInformation("强制更新密钥状态 - 分组: {GroupId}, 状态: {Status}", groupId, status);

            // 验证状态值
            if (status != "valid" && status != "invalid")
            {
                return new { success = false, error = "状态值必须是 'valid' 或 'invalid'" };
            }

            // 获取密钥哈希
            var keyHash = ComputeKeyHash(apiKey);
            var validation = await GetKeyValidationAsync(groupId, keyHash);

            var isValid = status == "valid";
            var currentTime = DateTime.Now;

            if (validation == null)
            {
                // 创建新的验证记录
                var providerType = await GetProviderTypeByGroupIdAsync(groupId);
                validation = new KeyValidation
                {
                    GroupId = groupId,
                    ApiKeyHash = keyHash,
                    ProviderType = providerType,
                    IsValid = isValid,
                    ErrorCount = isValid ? 0 : 1,
                    LastError = isValid ? "" : "管理员手动设置为无效",
                    LastStatusCode = null, // 手动设置状态时无HTTP状态码
                    LastValidatedAt = currentTime,
                    CreatedAt = currentTime
                };

                await _db.Insertable(validation).ExecuteCommandAsync();
                _logger.LogInformation("创建新的密钥验证记录 - 分组: {GroupId}, 状态: {Status}", groupId, status);
            }
            else
            {
                // 更新现有验证记录
                validation.IsValid = isValid;
                validation.ErrorCount = isValid ? 0 : validation.ErrorCount + 1;
                validation.LastError = isValid ? "" : "管理员手动设置为无效";
                validation.LastStatusCode = null; // 手动设置状态时无HTTP状态码
                validation.LastValidatedAt = currentTime;

                await _db.Updateable(validation).ExecuteCommandAsync();
                _logger.LogInformation("更新密钥验证记录 - 分组: {GroupId}, 状态: {Status}", groupId, status);
            }

            return new
            {
                success = true,
                message = $"成功将密钥状态强制更新为 {status}",
                api_key_hash = keyHash.Substring(0, 8),
                old_status = validation.IsValid ? "valid" : "invalid",
                new_status = status,
                updated_at = currentTime,
                error_count = validation.ErrorCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "强制更新密钥状态时发生异常 - 分组: {GroupId}", groupId);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// 获取分组的密钥使用统计
    /// </summary>
    public async Task<object> GetGroupKeyUsageStatsAsync(string groupId)
    {
        try
        {
            // 获取分组信息
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
            {
                return new { success = false, error = "分组不存在" };
            }

            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? [];
            var keyStats = new List<object>();

            // 处理每个密钥的统计信息（使用数据库数据）
            foreach (var apiKey in apiKeys)
            {
                var keyHash = ComputeKeyHash(apiKey);
                var stats = await GetKeyUsageStatsAsync(groupId, keyHash);
                var isAvailable = await IsKeyAvailableAsync(groupId, apiKey);
                
                // 获取密钥验证状态信息，包括最后检查的状态码
                var validation = await _db.Queryable<KeyValidation>()
                    .Where(kv => kv.GroupId == groupId && kv.ApiKeyHash == keyHash)
                    .FirstAsync();

                keyStats.Add(new
                {
                    api_key_hash = keyHash.Substring(0, 8),
                    api_key_prefix = apiKey.Substring(0, Math.Min(8, apiKey.Length)) + "****",
                    usage_count = stats?.UsageCount ?? 0,
                    last_used = stats?.LastUsedAt,
                    is_available = isAvailable,
                    last_status_code = validation?.LastStatusCode,
                    last_validated_at = validation?.LastValidatedAt,
                    created_at = stats?.CreatedAt,
                    updated_at = stats?.UpdatedAt
                });
            }

            return new
            {
                success = true,
                group_id = groupId,
                group_name = group.GroupName,
                balance_policy = group.BalancePolicy,
                total_keys = apiKeys.Count,
                key_stats = keyStats.OrderBy(k => ((dynamic)k).usage_count).ToList(), // 按使用次数排序
                stats_updated_at = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组密钥使用统计时发生异常 - 分组: {GroupId}", groupId);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// 更新密钥使用统计（数据库版本）
    /// </summary>
    public async Task UpdateKeyUsageStatsAsync(string groupId, string apiKey)
    {
        try
        {
            var keyHash = ComputeKeyHash(apiKey);
            var currentTime = DateTime.Now;

            // 查找现有记录
            var existingStats = await _db.Queryable<KeyUsageStats>()
                .Where(s => s.GroupId == groupId && s.ApiKeyHash == keyHash)
                .FirstAsync();

            if (existingStats != null)
            {
                // 更新现有记录
                existingStats.UsageCount++;
                existingStats.LastUsedAt = currentTime;
                existingStats.UpdatedAt = currentTime;

                await _db.Updateable(existingStats).ExecuteCommandAsync();
            }
            else
            {
                // 创建新记录
                var newStats = new KeyUsageStats
                {
                    GroupId = groupId,
                    ApiKeyHash = keyHash,
                    UsageCount = 1,
                    LastUsedAt = currentTime,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };

                await _db.Insertable(newStats).ExecuteCommandAsync();
            }

            _logger.LogDebug("更新密钥使用统计 - 分组: {GroupId}, 密钥: {KeyPrefix}",
                groupId, apiKey.Substring(0, Math.Min(8, apiKey.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新密钥使用统计时发生异常 - 分组: {GroupId}", groupId);
        }
    }

    /// <summary>
    /// 获取密钥使用统计（数据库版本）
    /// </summary>
    public async Task<KeyUsageStats?> GetKeyUsageStatsAsync(string groupId, string apiKeyHash)
    {
        try
        {
            return await _db.Queryable<KeyUsageStats>()
                .Where(s => s.GroupId == groupId && s.ApiKeyHash == apiKeyHash)
                .FirstAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取密钥使用统计时发生异常 - 分组: {GroupId}", groupId);
            return null;
        }
    }

    /// <summary>
    /// 检查和恢复无效密钥（后台服务使用）
    /// </summary>
    public async Task<object> CheckAndRecoverInvalidKeysAsync()
    {
        try
        {
            _logger.LogDebug("开始执行密钥恢复检查...");

            // 获取所有启用的分组
            var enabledGroups = await _db.Queryable<GroupConfig>()
                .Where(g => g.Enabled)
                .ToListAsync();

            if (!enabledGroups.Any())
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "没有找到启用的分组" },
                    { "checked_groups", 0 },
                    { "recovered_keys", 0 }
                };
            }

            var totalRecoveredKeys = 0;
            var checkedGroups = 0;
            var groupResults = new List<object>();

            foreach (var group in enabledGroups)
            {
                try
                {
                    checkedGroups++;
                    var (CheckedKeys, RecoveredKeys) = await CheckGroupInvalidKeysForRecoveryAsync(group);

                    if (RecoveredKeys > 0)
                    {
                        totalRecoveredKeys += RecoveredKeys;
                        groupResults.Add(new
                        {
                            group_id = group.Id,
                            group_name = group.GroupName,
                            provider_type = group.ProviderType,
                            checked_keys = CheckedKeys,
                            recovered_keys = RecoveredKeys
                        });

                        _logger.LogInformation("分组 {GroupName} (ID: {GroupId}) 恢复了 {RecoveredKeys} 个密钥",
                            group.GroupName, group.Id, RecoveredKeys);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "检查分组 {GroupId} 密钥恢复时发生异常", group.Id);
                }
            }

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "message", totalRecoveredKeys > 0 ?
                    $"成功恢复 {totalRecoveredKeys} 个密钥" :
                    "没有发现可恢复的密钥" },
                { "checked_groups", checkedGroups },
                { "recovered_keys", totalRecoveredKeys },
                { "check_time", DateTime.Now },
                { "group_details", groupResults }
            };

            if (totalRecoveredKeys > 0)
            {
                _logger.LogInformation("密钥恢复检查完成，共检查 {CheckedGroups} 个分组，恢复 {RecoveredKeys} 个密钥",
                    checkedGroups, totalRecoveredKeys);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查和恢复无效密钥时发生异常");
            return new Dictionary<string, object>
            {
                { "success", false },
                { "error", ex.Message },
                { "check_time", DateTime.Now }
            };
        }
    }

    /// <summary>
    /// 检查分组中的无效密钥是否恢复
    /// </summary>
    private async Task<(int CheckedKeys, int RecoveredKeys)> CheckGroupInvalidKeysForRecoveryAsync(GroupConfig group)
    {
        var checkedKeys = 0;
        var recoveredKeys = 0;

        try
        {
            // 获取该分组所有无效的密钥验证记录
            var invalidValidations = await _db.Queryable<KeyValidation>()
                .Where(kv => kv.GroupId == group.Id && !kv.IsValid)
                .ToListAsync();

            if (invalidValidations.Count == 0)
            {
                return (checkedKeys, recoveredKeys);
            }

            var apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? [];
            var provider = _providerFactory.GetProvider(group.ProviderType);

            foreach (var validation in invalidValidations)
            {
                try
                {
                    // 找到对应的API密钥
                    var matchingKey = apiKeys.FirstOrDefault(key =>
                        ComputeKeyHash(key) == validation.ApiKeyHash);

                    if (string.IsNullOrEmpty(matchingKey))
                    {
                        // 密钥可能已被删除，清理验证记录
                        await _db.Deleteable<KeyValidation>()
                            .Where(kv => kv.Id == validation.Id)
                            .ExecuteCommandAsync();
                        continue;
                    }

                    checkedKeys++;

                    // 重新验证密钥
                    var validationResult = await ValidateApiKeyWithProviderAsync(provider, matchingKey, group);

                    if (validationResult.IsValid)
                    {
                        // 密钥已恢复，重置错误状态
                        validation.IsValid = true;
                        validation.ErrorCount = 0;
                        validation.LastError = "";
                        validation.LastStatusCode = validationResult.StatusCode;
                        validation.LastValidatedAt = DateTime.Now;

                        await _db.Updateable(validation).ExecuteCommandAsync();
                        recoveredKeys++;

                        _logger.LogInformation("密钥已恢复正常 - 分组: {GroupId}, 密钥前缀: {KeyPrefix}",
                            group.Id, matchingKey.Substring(0, Math.Min(8, matchingKey.Length)) + "****");
                    }
                    else
                    {
                        // 仍然无效，更新最后验证时间但不增加错误计数
                        validation.LastValidatedAt = DateTime.Now;
                        await _db.Updateable(validation).ExecuteCommandAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "检查密钥恢复状态时发生异常 - 分组: {GroupId}, 验证ID: {ValidationId}",
                        group.Id, validation.Id);
                }

                // 在密钥检查之间添加延迟，避免过于频繁的API调用
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查分组 {GroupId} 的无效密钥恢复时发生异常", group.Id);
        }

        return (checkedKeys, recoveredKeys);
    }

    /// <summary>
    /// 清除所有状态码为401的无效密钥
    /// 从orch_groups、orch_key_validation、orch_key_usage_stats表中删除相关数据
    /// </summary>
    public async Task<object> ClearInvalidKeysAsync()
    {
        var clearStartTime = DateTime.Now;
        var clearedKeysCount = 0;
        var affectedGroups = new List<string>();
        var errors = new List<string>();

        try
        {
            _logger.LogInformation("开始清除状态码为401的无效密钥");

            // 1. 查找所有LastStatusCode为401的无效密钥验证记录
            var invalidValidations = await _db.Queryable<KeyValidation>()
                .Where(v => v.LastStatusCode == 401)
                .ToListAsync();

            if (!invalidValidations.Any())
            {
                _logger.LogInformation("未找到状态码为401的无效密钥");
                return new
                {
                    success = true,
                    message = "未找到需要清除的无效密钥",
                    cleared_keys_count = 0,
                    affected_groups = new List<string>(),
                    errors = new List<string>(),
                    operation_time = DateTime.Now,
                    duration_ms = (DateTime.Now - clearStartTime).TotalMilliseconds
                };
            }

            // 按分组分组处理
            var groupedValidations = invalidValidations.GroupBy(v => v.GroupId).ToList();
            _logger.LogInformation("找到 {Count} 个分组中的 {KeyCount} 个无效密钥需要清除",
                groupedValidations.Count, invalidValidations.Count);

            foreach (var groupValidations in groupedValidations)
            {
                var groupId = groupValidations.Key;
                var validationsInGroup = groupValidations.ToList();

                try
                {
                    // 获取分组信息
                    var group = await _db.Queryable<GroupConfig>()
                        .Where(g => g.Id == groupId)
                        .FirstAsync();

                    if (group == null)
                    {
                        var errorMsg = $"分组 {groupId} 不存在，跳过处理";
                        _logger.LogWarning(errorMsg);
                        errors.Add(errorMsg);
                        continue;
                    }

                    // 解析分组的API密钥列表
                    var groupApiKeys = new List<string>();
                    if (!string.IsNullOrEmpty(group.ApiKeys))
                    {
                        try
                        {
                            groupApiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"解析分组 {groupId} 的API密钥列表失败: {ex.Message}";
                            _logger.LogError(errorMsg);
                            errors.Add(errorMsg);
                            continue;
                        }
                    }

                    var keysToRemove = new List<string>();

                    // 找出需要移除的密钥
                    foreach (var validation in validationsInGroup)
                    {
                        // 通过哈希找到对应的原始密钥
                        foreach (var apiKey in groupApiKeys)
                        {
                            var keyHash = ComputeKeyHash(apiKey);
                            if (keyHash == validation.ApiKeyHash)
                            {
                                keysToRemove.Add(apiKey);
                                break;
                            }
                        }
                    }

                    if (keysToRemove.Any())
                    {
                        _logger.LogInformation("分组 {GroupId} ({GroupName}) 将移除 {Count} 个无效密钥",
                            groupId, group.GroupName, keysToRemove.Count);

                        // 从分组中移除无效密钥
                        var remainingKeys = groupApiKeys.Except(keysToRemove).ToList();
                        group.ApiKeys = JsonConvert.SerializeObject(remainingKeys);
                        group.UpdatedAt = DateTime.Now;

                        // 更新分组
                        await _db.Updateable(group).ExecuteCommandAsync();

                        // 删除密钥验证记录
                        var validationIds = validationsInGroup.Select(v => v.Id).ToList();
                        await _db.Deleteable<KeyValidation>()
                            .Where(v => validationIds.Contains(v.Id))
                            .ExecuteCommandAsync();

                        // 删除密钥使用统计记录
                        var keyHashes = validationsInGroup.Select(v => v.ApiKeyHash).ToList();
                        await _db.Deleteable<KeyUsageStats>()
                            .Where(s => s.GroupId == groupId && keyHashes.Contains(s.ApiKeyHash))
                            .ExecuteCommandAsync();

                        clearedKeysCount += keysToRemove.Count;
                        affectedGroups.Add($"{group.GroupName} ({groupId})");

                        _logger.LogInformation("成功清除分组 {GroupId} 中的 {Count} 个无效密钥",
                            groupId, keysToRemove.Count);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"处理分组 {groupId} 时发生异常: {ex.Message}";
                    _logger.LogError(ex, errorMsg);
                    errors.Add(errorMsg);
                }
            }

            var duration = (DateTime.Now - clearStartTime).TotalMilliseconds;
            var result = new
            {
                success = true,
                message = $"清除操作完成，共移除 {clearedKeysCount} 个无效密钥",
                cleared_keys_count = clearedKeysCount,
                affected_groups = affectedGroups,
                errors = errors,
                operation_time = clearStartTime,
                duration_ms = duration
            };

            _logger.LogInformation("无效密钥清除操作完成 - 清除数量: {Count}, 受影响分组: {Groups}, 耗时: {Duration}ms",
                clearedKeysCount, affectedGroups.Count, duration);

            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"清除无效密钥时发生异常: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            errors.Add(errorMsg);

            return new
            {
                success = false,
                message = errorMsg,
                cleared_keys_count = clearedKeysCount,
                affected_groups = affectedGroups,
                errors = errors,
                operation_time = clearStartTime,
                duration_ms = (DateTime.Now - clearStartTime).TotalMilliseconds
            };
        }
    }

    public async Task<object> ClearEmptyGroupsAsync()
    {
        var operationStartTime = DateTime.Now;
        var clearedGroupsCount = 0;
        var errors = new List<string>();

        try
        {
            _logger.LogInformation("开始清除空白密钥的服务商分组操作");

            // 获取所有未删除的分组
            var allGroups = await _db.Queryable<GroupConfig>()
                .Where(g => !g.IsDeleted)
                .ToListAsync();

            var emptyGroups = new List<GroupConfig>();

            // 检查每个分组是否有密钥
            foreach (var group in allGroups)
            {
                try
                {
                    var apiKeys = new List<string>();
                    if (!string.IsNullOrEmpty(group.ApiKeys))
                    {
                        try
                        {
                            apiKeys = JsonConvert.DeserializeObject<List<string>>(group.ApiKeys) ?? new List<string>();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("分组 {GroupId} 的API密钥反序列化失败: {Error}", group.Id, ex.Message);
                            // 如果反序列化失败，认为是空分组
                            apiKeys = new List<string>();
                        }
                    }

                    // 如果分组没有密钥或者密钥列表为空，则标记为删除
                    if (!apiKeys.Any() || apiKeys.All(string.IsNullOrWhiteSpace))
                    {
                        emptyGroups.Add(group);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"检查分组 {group.Id} 时出错: {ex.Message}");
                    _logger.LogError(ex, "检查分组 {GroupId} 时发生异常", group.Id);
                }
            }

            // 标记删除空白分组
            if (emptyGroups.Any())
            {
                foreach (var emptyGroup in emptyGroups)
                {
                    try
                    {
                        await _db.Updateable<GroupConfig>()
                            .SetColumns(g => new GroupConfig
                            {
                                IsDeleted = true,
                                UpdatedAt = DateTime.Now
                            })
                            .Where(g => g.Id == emptyGroup.Id)
                            .ExecuteCommandAsync();

                        clearedGroupsCount++;
                        _logger.LogInformation("分组 {GroupId} ({GroupName}) 已标记为删除", emptyGroup.Id, emptyGroup.GroupName);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"删除分组 {emptyGroup.Id} 时出错: {ex.Message}");
                        _logger.LogError(ex, "删除分组 {GroupId} 时发生异常", emptyGroup.Id);
                    }
                }
            }

            var duration = (DateTime.Now - operationStartTime).TotalMilliseconds;

            var result = new
            {
                success = true,
                message = $"成功清除 {clearedGroupsCount} 个空白密钥分组",
                cleared_groups_count = clearedGroupsCount,
                cleared_groups = emptyGroups.Select(g => new
                {
                    id = g.Id,
                    name = g.GroupName,
                    provider_type = g.ProviderType
                }).ToList(),
                total_groups_checked = allGroups.Count,
                errors = errors,
                operation_time = operationStartTime,
                duration_ms = duration
            };

            _logger.LogInformation("空白分组清除操作完成 - 清除数量: {Count}, 检查分组总数: {Total}, 耗时: {Duration}ms",
                clearedGroupsCount, allGroups.Count, duration);

            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"清除空白分组时发生异常: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            errors.Add(errorMsg);

            return new
            {
                success = false,
                message = errorMsg,
                cleared_groups_count = clearedGroupsCount,
                errors = errors,
                operation_time = operationStartTime,
                duration_ms = (DateTime.Now - operationStartTime).TotalMilliseconds
            };
        }
    }
}