using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 版本管理服务实现
/// </summary>
public class VersionService : IVersionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VersionService> _logger;
    private readonly IConfiguration _configuration;
    private const string GitHubApiUrl = "https://cdn.gh-proxy.com/https://api.github.com/repos/xiaoyutx94/OrchestrationApi/releases/latest";
    private const string GitHubReleasesUrl = "https://cdn.gh-proxy.com/https://github.com/xiaoyutx94/OrchestrationApi/releases";

    public VersionService(
        HttpClient httpClient,
        ILogger<VersionService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // 设置GitHub API请求头（避免重复添加）
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OrchestrationApi/1.0");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
    }

    /// <summary>
    /// 获取当前应用版本
    /// </summary>
    public string GetCurrentVersion()
    {
        try
        {
            // 尝试从配置文件获取版本号
            var configVersion = _configuration["OrchestrationApi:Version"];
            if (!string.IsNullOrEmpty(configVersion))
            {
                return configVersion;
            }

            // 从程序集获取版本号
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取当前版本失败，使用默认版本");
            return "1.0.0";
        }
    }

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    public async Task<VersionCheckResult> CheckForUpdatesAsync()
    {
        var result = new VersionCheckResult
        {
            CurrentVersion = GetCurrentVersion(),
            ReleaseUrl = GitHubReleasesUrl
        };

        // 只在生产环境检查版本更新
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("开发环境跳过版本检查");
            return result;
        }

        try
        {
            var latestRelease = await GetLatestReleaseAsync();
            if (latestRelease != null)
            {
                result.LatestVersion = latestRelease.TagName;
                result.PublishedAt = latestRelease.PublishedAt;
                result.ReleaseNotes = latestRelease.Body;
                result.ReleaseUrl = latestRelease.HtmlUrl;

                // 比较版本号
                result.HasNewVersion = IsNewerVersion(result.CurrentVersion, latestRelease.TagName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查版本更新失败");
            result.ErrorMessage = "无法检查版本更新: " + ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 获取最新版本信息
    /// </summary>
    public async Task<GitHubRelease?> GetLatestReleaseAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(GitHubApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API请求失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GitHub API响应: {Json}", json.Substring(0, Math.Min(500, json.Length)));

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var apiResponse = JsonSerializer.Deserialize<GitHubApiResponse>(json, options);
            if (apiResponse == null)
            {
                _logger.LogWarning("无法解析GitHub API响应");
                return null;
            }

            return new GitHubRelease
            {
                TagName = apiResponse.TagName ?? "v1.0.0",
                Name = apiResponse.Name ?? "Release",
                Body = apiResponse.Body ?? "",
                Prerelease = apiResponse.Prerelease,
                PublishedAt = apiResponse.PublishedAt,
                HtmlUrl = apiResponse.HtmlUrl ?? GitHubReleasesUrl,
                Url = apiResponse.Url ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最新版本信息失败");
            return null;
        }
    }

    /// <summary>
    /// 比较版本号，判断是否有新版本
    /// </summary>
    private bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        try
        {
            // 移除版本号前的'v'前缀并清理格式
            var current = CleanVersion(currentVersion);
            var latest = CleanVersion(latestVersion);

            _logger.LogDebug("版本比较: 当前 {Current} vs 最新 {Latest}", current, latest);

            // 使用Version类进行比较
            if (Version.TryParse(current, out var currentVer) &&
                Version.TryParse(latest, out var latestVer))
            {
                return latestVer > currentVer;
            }

            // 如果解析失败，使用字符串比较
            return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "版本号比较失败: {Current} vs {Latest}", currentVersion, latestVersion);
            return false;
        }
    }

    /// <summary>
    /// 清理版本号格式
    /// </summary>
    private string CleanVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return "1.0.0";

        // 移除v前缀
        var cleaned = version.TrimStart('v', 'V');

        // 移除.0.0后缀（如果是4段版本号）
        if (cleaned.Count(c => c == '.') == 3)
        {
            var parts = cleaned.Split('.');
            if (parts.Length == 4 && parts[3] == "0")
            {
                cleaned = string.Join(".", parts.Take(3));
            }
        }

        return cleaned;
    }
}

/// <summary>
/// GitHub API响应模型
/// </summary>
internal class GitHubApiResponse
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}