namespace OrchestrationApi.Models;

/// <summary>
/// 健康检查分析结果
/// </summary>
public class HealthCheckAnalysis
{
    /// <summary>
    /// 服务商是否健康
    /// </summary>
    public bool ProviderHealthy { get; set; }

    /// <summary>
    /// 密钥是否健康
    /// </summary>
    public bool KeysHealthy { get; set; }

    /// <summary>
    /// 模型是否健康
    /// </summary>
    public bool ModelsHealthy { get; set; }

    /// <summary>
    /// 是否存在不一致的状态
    /// </summary>
    public bool IsInconsistent { get; set; }

    /// <summary>
    /// 不一致的原因说明
    /// </summary>
    public string? InconsistencyReason { get; set; }

    /// <summary>
    /// 总检查次数
    /// </summary>
    public int TotalChecks { get; set; }

    /// <summary>
    /// 成功检查次数
    /// </summary>
    public int SuccessfulChecks { get; set; }

    /// <summary>
    /// 失败检查次数
    /// </summary>
    public int FailedChecks { get; set; }

    /// <summary>
    /// 整体健康状态
    /// </summary>
    public bool IsOverallHealthy => ProviderHealthy && KeysHealthy && ModelsHealthy;

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks * 100 : 0;

    /// <summary>
    /// 获取状态摘要
    /// </summary>
    public string GetStatusSummary()
    {
        if (IsOverallHealthy)
        {
            return "所有组件健康";
        }

        var issues = new List<string>();
        if (!ProviderHealthy) issues.Add("服务商异常");
        if (!KeysHealthy) issues.Add("密钥异常");
        if (!ModelsHealthy) issues.Add("模型异常");

        var summary = string.Join("、", issues);
        
        if (IsInconsistent)
        {
            summary += " (状态不一致)";
        }

        return summary;
    }

    /// <summary>
    /// 获取详细的状态说明
    /// </summary>
    public string GetDetailedExplanation()
    {
        if (IsOverallHealthy)
        {
            return "所有健康检查都通过，系统运行正常。";
        }

        var explanation = GetStatusSummary();
        
        if (!string.IsNullOrEmpty(InconsistencyReason))
        {
            explanation += "\n\n详细说明：" + InconsistencyReason;
        }

        if (IsInconsistent)
        {
            explanation += "\n\n建议：检查不同端点的配置和可用性，确保服务商的所有API端点都正常工作。";
        }

        return explanation;
    }
}
