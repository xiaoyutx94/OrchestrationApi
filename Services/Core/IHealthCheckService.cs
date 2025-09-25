using OrchestrationApi.Models;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 健康检查服务接口
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// 执行服务商健康检查
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="apiKey">API密钥（可选，用于认证检查）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果</returns>
    Task<HealthCheckResult> CheckProviderHealthAsync(string groupId, string? apiKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行API密钥验证检查
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="apiKey">API密钥</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果</returns>
    Task<HealthCheckResult> CheckApiKeyHealthAsync(string groupId, string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行模型可用性检查
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="apiKey">API密钥</param>
    /// <param name="modelId">模型ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果</returns>
    Task<HealthCheckResult> CheckModelHealthAsync(string groupId, string apiKey, string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量执行分组的完整健康检查
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果列表</returns>
    Task<List<HealthCheckResult>> CheckGroupCompleteHealthAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行所有启用分组的健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果列表</returns>
    Task<List<HealthCheckResult>> CheckAllGroupsHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存健康检查结果
    /// </summary>
    /// <param name="result">健康检查结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveHealthCheckResultAsync(HealthCheckResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量保存健康检查结果
    /// </summary>
    /// <param name="results">健康检查结果列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveHealthCheckResultsAsync(List<HealthCheckResult> results, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新健康检查统计信息
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="checkType">检查类型</param>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="responseTimeMs">响应时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateHealthCheckStatsAsync(string groupId, string checkType, bool isSuccess, int responseTimeMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分组的健康检查统计信息
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="checkType">检查类型（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查统计信息</returns>
    Task<List<HealthCheckStats>> GetHealthCheckStatsAsync(string groupId, string? checkType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有分组的健康检查统计信息
    /// </summary>
    /// <param name="checkType">检查类型（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查统计信息</returns>
    Task<List<HealthCheckStats>> GetAllHealthCheckStatsAsync(string? checkType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分组的最近健康检查结果
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="checkType">检查类型（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果列表</returns>
    Task<List<HealthCheckResult>> GetRecentHealthCheckResultsAsync(string groupId, string? checkType = null, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期的健康检查记录
    /// </summary>
    /// <param name="retentionDays">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的记录数量</returns>
    Task<int> CleanupExpiredHealthCheckRecordsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分析健康检查结果的一致性
    /// </summary>
    /// <param name="results">健康检查结果列表</param>
    /// <returns>状态分析结果</returns>
    HealthCheckAnalysis AnalyzeHealthCheckConsistency(List<HealthCheckResult> results);
}

/// <summary>
/// 健康检查类型常量
/// </summary>
public static class HealthCheckTypes
{
    /// <summary>
    /// 服务商健康检查
    /// </summary>
    public const string Provider = "provider";

    /// <summary>
    /// API密钥验证检查
    /// </summary>
    public const string ApiKey = "key";

    /// <summary>
    /// 模型可用性检查
    /// </summary>
    public const string Model = "model";
}

/// <summary>
/// 健康检查结果扩展方法
/// </summary>
public static class HealthCheckResultExtensions
{
    /// <summary>
    /// 判断健康检查是否成功
    /// </summary>
    /// <param name="result">健康检查结果</param>
    /// <returns>是否成功</returns>
    public static bool IsHealthy(this HealthCheckResult result)
    {
        return result.IsSuccess && result.StatusCode >= 200 && result.StatusCode < 300;
    }

    /// <summary>
    /// 获取健康状态描述
    /// </summary>
    /// <param name="result">健康检查结果</param>
    /// <returns>状态描述</returns>
    public static string GetHealthStatusDescription(this HealthCheckResult result)
    {
        if (result.IsHealthy())
        {
            return "健康";
        }

        return result.StatusCode switch
        {
            401 => "密钥无效",
            403 => "访问被拒绝",
            404 => "服务不存在",
            429 => "请求限流",
            500 => "服务器内部错误",
            503 => "服务不可用",
            _ => $"错误 ({result.StatusCode})"
        };
    }
}
