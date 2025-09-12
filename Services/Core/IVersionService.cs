namespace OrchestrationApi.Services.Core;

/// <summary>
/// 版本管理服务接口
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// 获取当前应用版本
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    Task<VersionCheckResult> CheckForUpdatesAsync();

    /// <summary>
    /// 获取最新版本信息
    /// </summary>
    Task<GitHubRelease?> GetLatestReleaseAsync();
}

/// <summary>
/// 版本检查结果
/// </summary>
public class VersionCheckResult
{
    /// <summary>
    /// 当前版本
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// 最新版本
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// 是否有新版本
    /// </summary>
    public bool HasNewVersion { get; set; }

    /// <summary>
    /// 版本发布页面URL
    /// </summary>
    public string? ReleaseUrl { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// 发布说明
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// GitHub Release 信息
/// </summary>
public class GitHubRelease
{
    /// <summary>
    /// 标签名称（版本号）
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// 发布名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 发布说明
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool Prerelease { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// HTML页面URL
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// API URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
