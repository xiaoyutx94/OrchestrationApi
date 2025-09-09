namespace OrchestrationApi.Services.Providers;

/// <summary>
/// 服务商工厂接口
/// </summary>
public interface IProviderFactory
{
    /// <summary>
    /// 获取指定类型的服务商实例
    /// </summary>
    ILLMProvider GetProvider(string providerType);

    /// <summary>
    /// 获取所有已注册的服务商类型
    /// </summary>
    IEnumerable<string> GetSupportedProviderTypes();
}

/// <summary>
/// 服务商工厂实现
/// </summary>
public class ProviderFactory : IProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProviderFactory> _logger;

    public ProviderFactory(IServiceProvider serviceProvider, ILogger<ProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ILLMProvider GetProvider(string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => _serviceProvider.GetRequiredService<OpenAiProvider>(),
            "anthropic" => _serviceProvider.GetRequiredService<AnthropicProvider>(),
            "gemini" => _serviceProvider.GetRequiredService<GeminiProvider>(),
            _ => throw new NotSupportedException($"Provider '{providerType}' is not supported."),
        };
    }

    public IEnumerable<string> GetSupportedProviderTypes()
    {
        return new[] { "openai", "anthropic", "gemini" };
    }
}