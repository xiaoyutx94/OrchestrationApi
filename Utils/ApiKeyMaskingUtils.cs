using System.Security.Cryptography;
using System.Text;

namespace OrchestrationApi.Utils;

/// <summary>
/// API密钥掩码工具类
/// 提供API密钥的掩码和哈希功能
/// </summary>
public static class ApiKeyMaskingUtils
{
    /// <summary>
    /// 对API密钥进行掩码处理
    /// 保留前4位和后4位字符，中间部分用星号(*)替代
    /// </summary>
    /// <param name="apiKey">原始API密钥</param>
    /// <returns>掩码后的API密钥</returns>
    public static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "****";

        // 如果密钥长度小于等于8位，全部用星号替代（保护短密钥）
        if (apiKey.Length <= 8)
            return new string('*', apiKey.Length);

        // 保留前4位和后4位，中间用星号替代
        var prefix = apiKey.Substring(0, 4);
        var suffix = apiKey.Substring(apiKey.Length - 4);
        var maskLength = apiKey.Length - 8;
        var mask = new string('*', maskLength);

        return $"{prefix}{mask}{suffix}";
    }

    /// <summary>
    /// 计算API密钥的SHA256哈希值
    /// </summary>
    /// <param name="apiKey">原始API密钥</param>
    /// <returns>哈希值（小写十六进制字符串）</returns>
    public static string ComputeKeyHash(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLower();
    }

    /// <summary>
    /// 验证掩码格式是否正确
    /// </summary>
    /// <param name="maskedKey">掩码后的密钥</param>
    /// <returns>是否为有效的掩码格式</returns>
    public static bool IsValidMaskedKey(string maskedKey)
    {
        if (string.IsNullOrEmpty(maskedKey))
            return false;

        // 检查是否包含星号
        if (!maskedKey.Contains('*'))
            return false;

        // 如果长度大于8，检查前4位和后4位是否不是星号
        if (maskedKey.Length > 8)
        {
            var prefix = maskedKey.Substring(0, 4);
            var suffix = maskedKey.Substring(maskedKey.Length - 4);
            
            return !prefix.Contains('*') && !suffix.Contains('*');
        }

        return true;
    }

    /// <summary>
    /// 从掩码密钥中提取前缀（用于显示和识别）
    /// </summary>
    /// <param name="maskedKey">掩码后的密钥</param>
    /// <returns>密钥前缀</returns>
    public static string GetKeyPrefix(string maskedKey)
    {
        if (string.IsNullOrEmpty(maskedKey))
            return "unknown";

        if (maskedKey.Length <= 8)
            return maskedKey.Substring(0, Math.Min(4, maskedKey.Length));

        return maskedKey.Substring(0, 4);
    }

    /// <summary>
    /// 批量处理API密钥掩码
    /// </summary>
    /// <param name="apiKeys">API密钥列表</param>
    /// <returns>掩码和哈希的映射字典</returns>
    public static Dictionary<string, (string MaskedKey, string Hash)> ProcessApiKeys(IEnumerable<string> apiKeys)
    {
        var result = new Dictionary<string, (string MaskedKey, string Hash)>();

        foreach (var apiKey in apiKeys)
        {
            if (string.IsNullOrEmpty(apiKey))
                continue;

            var maskedKey = MaskApiKey(apiKey);
            var hash = ComputeKeyHash(apiKey);
            
            result[apiKey] = (maskedKey, hash);
        }

        return result;
    }
}
