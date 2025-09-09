using Newtonsoft.Json;
using OrchestrationApi.Models;
using System.Net;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 代理HttpClient服务，为不同的分组创建支持代理的HttpClient
/// </summary>
public interface IProxyHttpClientService
{
    /// <summary>
    /// 为指定分组创建HttpClient
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <returns>HttpClient实例</returns>
    HttpClient CreateHttpClient(GroupConfig groupConfig);

    /// <summary>
    /// 为指定分组创建HttpClient（支持自定义连接超时）
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpClient实例</returns>
    HttpClient CreateHttpClient(GroupConfig groupConfig, int connectionTimeoutSeconds);

    /// <summary>
    /// 为指定的代理配置创建HttpClient
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <returns>HttpClient实例</returns>
    HttpClient CreateHttpClient(ProxyConfiguration? proxyConfig);

    /// <summary>
    /// 为指定的代理配置创建HttpClient（支持自定义连接超时）
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpClient实例</returns>
    HttpClient CreateHttpClient(ProxyConfiguration? proxyConfig, int connectionTimeoutSeconds);
}

/// <summary>
/// 代理HttpClient服务实现
/// </summary>
public class ProxyHttpClientService : IProxyHttpClientService, IDisposable
{
    private readonly ILogger<ProxyHttpClientService> _logger;
    private readonly Dictionary<string, HttpClient> _httpClients = new();
    private readonly Dictionary<string, HttpMessageHandler> _handlers = new();
    private readonly IConfiguration _configuration;
    private readonly object _lock = new object();
    private bool _disposed = false;

    public ProxyHttpClientService(ILogger<ProxyHttpClientService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 为指定分组创建HttpClient
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <returns>HttpClient实例</returns>
    public HttpClient CreateHttpClient(GroupConfig groupConfig)
    {
        // 使用默认连接超时时间
        var defaultConnectionTimeout = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30);
        return CreateHttpClient(groupConfig, defaultConnectionTimeout);
    }

    /// <summary>
    /// 为指定分组创建HttpClient（支持自定义连接超时）
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpClient实例</returns>
    public HttpClient CreateHttpClient(GroupConfig groupConfig, int connectionTimeoutSeconds)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProxyHttpClientService));

        var cacheKey = GenerateCacheKey(groupConfig, connectionTimeoutSeconds);

        lock (_lock)
        {
            if (_httpClients.TryGetValue(cacheKey, out var existingClient))
            {
                return existingClient;
            }

            // 解析代理配置
            ProxyConfiguration? proxyConfig = null;
            if (groupConfig.ProxyEnabled && !string.IsNullOrEmpty(groupConfig.ProxyConfig))
            {
                try
                {
                    proxyConfig = JsonConvert.DeserializeObject<ProxyConfiguration>(groupConfig.ProxyConfig);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析分组 {GroupId} 的代理配置失败，将使用无代理的HttpClient", groupConfig.Id);
                }
            }

            var httpClient = CreateHttpClientInternal(proxyConfig, cacheKey, connectionTimeoutSeconds);
            _httpClients[cacheKey] = httpClient;
            _logger.LogDebug("创建新的HttpClient实例，缓存键: {CacheKey}, 连接超时: {ConnectionTimeout}秒", cacheKey, connectionTimeoutSeconds);
            return httpClient;
        }
    }

    /// <summary>
    /// 为指定的代理配置创建HttpClient
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <returns>HttpClient实例</returns>
    public HttpClient CreateHttpClient(ProxyConfiguration? proxyConfig)
    {
        // 使用默认连接超时时间
        var defaultConnectionTimeout = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30);
        return CreateHttpClient(proxyConfig, defaultConnectionTimeout);
    }

    /// <summary>
    /// 为指定的代理配置创建HttpClient（支持自定义连接超时）
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpClient实例</returns>
    public HttpClient CreateHttpClient(ProxyConfiguration? proxyConfig, int connectionTimeoutSeconds)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProxyHttpClientService));

        var cacheKey = GenerateCacheKey(proxyConfig, connectionTimeoutSeconds);

        lock (_lock)
        {
            if (_httpClients.TryGetValue(cacheKey, out var existingClient))
            {
                return existingClient;
            }

            var httpClient = CreateHttpClientInternal(proxyConfig, cacheKey, connectionTimeoutSeconds);
            _httpClients[cacheKey] = httpClient;
            return httpClient;
        }
    }

    /// <summary>
    /// 内部创建HttpClient实现
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpClient实例</returns>
    private HttpClient CreateHttpClientInternal(ProxyConfiguration? proxyConfig, string cacheKey, int connectionTimeoutSeconds = 30)
    {
        HttpMessageHandler handler;

        if (proxyConfig != null && !string.IsNullOrEmpty(proxyConfig.Host))
        {
            // 创建支持代理的Handler
            handler = CreateProxyHandler(proxyConfig, connectionTimeoutSeconds);
            _logger.LogInformation("为分组创建代理HttpClient: {ProxyType}://{ProxyHost}:{ProxyPort}, 连接超时: {ConnectionTimeout}秒",
                proxyConfig.Type, proxyConfig.Host, proxyConfig.Port, connectionTimeoutSeconds);
        }
        else
        {
            // 创建普通的Handler
            handler = CreateStandardHandler(connectionTimeoutSeconds);
            _logger.LogDebug("创建无代理的HttpClient, 连接超时: {ConnectionTimeout}秒", connectionTimeoutSeconds);
        }

        // 缓存Handler以便后续释放
        _handlers[cacheKey] = handler;

        var httpClient = new HttpClient(handler, false); // 不自动释放handler

        // 设置基本配置 - 注意：这里设置一个很大的值，实际超时通过CancellationToken控制
        httpClient.Timeout = TimeSpan.FromHours(1); // 设置较大的超时，避免HttpClient自身超时干扰
        httpClient.DefaultRequestHeaders.Add("User-Agent", "OrchestrationApi/1.0");

        return httpClient;
    }

    /// <summary>
    /// 创建标准Handler（无代理）
    /// </summary>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpMessageHandler</returns>
    private HttpMessageHandler CreateStandardHandler(int connectionTimeoutSeconds)
    {
        var handler = new HttpClientHandler();
        
        // 注意：HttpClientHandler不直接支持ConnectTimeout属性
        // 连接超时通过SocketsHttpHandler实现，但为了向后兼容，这里使用HttpClientHandler
        // 实际的连接超时通过CancellationToken在各Provider中控制
        
        return handler;
    }

    /// <summary>
    /// 创建代理Handler
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>HttpMessageHandler</returns>
    private HttpMessageHandler CreateProxyHandler(ProxyConfiguration proxyConfig, int connectionTimeoutSeconds)
    {
        var handler = new HttpClientHandler();

        try
        {
            // 构建代理URI
            var proxyUriBuilder = new UriBuilder();
            
            switch (proxyConfig.Type.ToLower())
            {
                case "http":
                    proxyUriBuilder.Scheme = "http";
                    break;
                case "https":
                    proxyUriBuilder.Scheme = "https";
                    break;
                case "socks5":
                    // SOCKS5代理在.NET中需要特殊处理，这里先使用HTTP
                    proxyUriBuilder.Scheme = "http";
                    _logger.LogWarning("SOCKS5代理暂不完全支持，将尝试使用HTTP代理");
                    break;
                default:
                    proxyUriBuilder.Scheme = "http";
                    break;
            }

            proxyUriBuilder.Host = proxyConfig.Host;
            proxyUriBuilder.Port = proxyConfig.Port;

            // 设置认证信息
            if (!string.IsNullOrEmpty(proxyConfig.Username))
            {
                proxyUriBuilder.UserName = proxyConfig.Username;
                if (!string.IsNullOrEmpty(proxyConfig.Password))
                {
                    proxyUriBuilder.Password = proxyConfig.Password;
                }
            }

            var proxyUri = proxyUriBuilder.Uri;
            var webProxy = new WebProxy(proxyUri);

            // 设置绕过本地地址
            if (proxyConfig.BypassLocal)
            {
                webProxy.BypassProxyOnLocal = true;
            }

            // 设置绕过域名列表
            if (proxyConfig.BypassDomains != null && proxyConfig.BypassDomains.Count > 0)
            {
                webProxy.BypassList = proxyConfig.BypassDomains.ToArray();
            }

            handler.Proxy = webProxy;
            handler.UseProxy = true;

            // 注意：HttpClientHandler不直接支持ConnectTimeout属性
            // 连接超时通过SocketsHttpHandler实现，但为了向后兼容，这里使用HttpClientHandler
            // 实际的连接超时通过CancellationToken在各Provider中控制

            _logger.LogDebug("创建代理Handler成功: {ProxyUri}, BypassLocal: {BypassLocal}, BypassDomains: {BypassDomains}, 连接超时: {ConnectionTimeout}秒",
                proxyUri.ToString().Replace(proxyConfig.Password ?? "", "***"), // 隐藏密码
                proxyConfig.BypassLocal,
                string.Join(", ", proxyConfig.BypassDomains ?? new List<string>()),
                connectionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建代理Handler失败，将使用无代理Handler");
            handler.UseProxy = false;
        }

        return handler;
    }

    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <returns>缓存键</returns>
    private string GenerateCacheKey(GroupConfig groupConfig)
    {
        // 使用默认连接超时时间
        var defaultConnectionTimeout = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30);
        return GenerateCacheKey(groupConfig, defaultConnectionTimeout);
    }

    /// <summary>
    /// 生成缓存键（支持连接超时）
    /// </summary>
    /// <param name="groupConfig">分组配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>缓存键</returns>
    private string GenerateCacheKey(GroupConfig groupConfig, int connectionTimeoutSeconds)
    {
        if (!groupConfig.ProxyEnabled || string.IsNullOrEmpty(groupConfig.ProxyConfig))
        {
            return $"no-proxy-timeout-{connectionTimeoutSeconds}";
        }

        // 使用代理配置的哈希值作为缓存键的一部分
        var hash = groupConfig.ProxyConfig.GetHashCode();
        return $"proxy-{groupConfig.Id}-{hash}-timeout-{connectionTimeoutSeconds}";
    }

    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <returns>缓存键</returns>
    private string GenerateCacheKey(ProxyConfiguration? proxyConfig)
    {
        // 使用默认连接超时时间
        var defaultConnectionTimeout = _configuration.GetValue<int>("OrchestrationApi:Global:ConnectionTimeout", 30);
        return GenerateCacheKey(proxyConfig, defaultConnectionTimeout);
    }

    /// <summary>
    /// 生成缓存键（支持连接超时）
    /// </summary>
    /// <param name="proxyConfig">代理配置</param>
    /// <param name="connectionTimeoutSeconds">连接超时时间（秒）</param>
    /// <returns>缓存键</returns>
    private string GenerateCacheKey(ProxyConfiguration? proxyConfig, int connectionTimeoutSeconds)
    {
        if (proxyConfig == null || string.IsNullOrEmpty(proxyConfig.Host))
        {
            return $"no-proxy-timeout-{connectionTimeoutSeconds}";
        }

        var keyData = $"{proxyConfig.Type}-{proxyConfig.Host}-{proxyConfig.Port}-{proxyConfig.Username}";
        var hash = keyData.GetHashCode();
        return $"proxy-config-{hash}-timeout-{connectionTimeoutSeconds}";
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            foreach (var client in _httpClients.Values)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放HttpClient时发生异常");
                }
            }

            foreach (var handler in _handlers.Values)
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放HttpMessageHandler时发生异常");
                }
            }

            _httpClients.Clear();
            _handlers.Clear();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~ProxyHttpClientService()
    {
        Dispose();
    }
}