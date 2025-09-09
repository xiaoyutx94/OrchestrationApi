using System;
using System.Threading;
using System.Threading.Tasks;
using System.ClientModel;

namespace OrchestrationApi.Services.Core;

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 执行带重试的异步操作
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带重试的异步操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        Func<int, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断异常是否应该重试
    /// </summary>
    bool ShouldRetry(Exception exception, int attemptNumber);

    /// <summary>
    /// 计算重试延迟
    /// </summary>
    TimeSpan CalculateDelay(int attemptNumber, Exception? lastException = null);
}

/// <summary>
/// 重试策略配置
/// </summary>
public class RetryPolicyConfig
{
    /// <summary>
    /// 最大重试次数（默认3次）
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 初始延迟时间（默认1秒）
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 最大延迟时间（默认30秒）
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 退避乘数（默认2.0，指数退避）
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// 添加随机抖动以避免惊群效应
    /// </summary>
    public bool EnableJitter { get; set; } = true;

    /// <summary>
    /// 可重试的HTTP状态码
    /// </summary>
    public HashSet<int> RetryableHttpStatusCodes { get; set; } = new()
    {
        429, // Too Many Requests (Rate Limiting)
        500, // Internal Server Error
        502, // Bad Gateway
        503, // Service Unavailable
        504, // Gateway Timeout
        408, // Request Timeout
        522, // Connection Timed Out
        524  // Timeout Occurred
    };

    /// <summary>
    /// 不可重试的HTTP状态码
    /// </summary>
    public HashSet<int> NonRetryableHttpStatusCodes { get; set; } = new()
    {
        400, // Bad Request
        401, // Unauthorized
        403, // Forbidden
        404, // Not Found
        405, // Method Not Allowed
        406, // Not Acceptable
        409, // Conflict
        410, // Gone
        413, // Payload Too Large
        414, // URI Too Long
        415, // Unsupported Media Type
        422, // Unprocessable Entity
        451  // Unavailable For Legal Reasons
    };

    /// <summary>
    /// 可重试的异常类型
    /// </summary>
    public HashSet<Type> RetryableExceptionTypes { get; set; } = new()
    {
        typeof(TaskCanceledException),  // 超时
        typeof(TimeoutException),       // 超时
        typeof(HttpRequestException),   // HTTP请求异常
        typeof(OperationCanceledException) // 操作取消（超时引起）
    };
}

/// <summary>
/// 智能重试策略实现
/// </summary>
public class IntelligentRetryPolicy : IRetryPolicy
{
    private readonly RetryPolicyConfig _config;
    private readonly ILogger<IntelligentRetryPolicy> _logger;
    private readonly Random _random;

    public IntelligentRetryPolicy(
        RetryPolicyConfig config,
        ILogger<IntelligentRetryPolicy> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    public async Task<T> ExecuteAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("执行操作，尝试次数: {Attempt}/{MaxRetries}", 
                    attempt + 1, _config.MaxRetries + 1);
                
                return await operation(attempt, cancellationToken);
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                lastException = ex;
                
                _logger.LogWarning(ex, "操作失败，将在 {Delay} 后重试，尝试次数: {Attempt}/{MaxRetries}, 异常: {ExceptionType}", 
                    CalculateDelay(attempt, ex), attempt + 1, _config.MaxRetries + 1, ex.GetType().Name);
                
                if (attempt < _config.MaxRetries)
                {
                    var delay = CalculateDelay(attempt, ex);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "操作失败，不可重试的异常: {ExceptionType}", ex.GetType().Name);
                throw;
            }
        }

        _logger.LogError("操作失败，已达最大重试次数 {MaxRetries}", _config.MaxRetries);
        throw lastException ?? new InvalidOperationException("操作失败，原因未知");
    }

    public async Task ExecuteAsync(
        Func<int, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object>(async (attempt, ct) =>
        {
            await operation(attempt, ct);
            return null!;
        }, cancellationToken);
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= _config.MaxRetries)
        {
            _logger.LogDebug("达到最大重试次数，不再重试");
            return false;
        }

        // 检查是否为可重试的异常类型
        var exceptionType = exception.GetType();
        if (!_config.RetryableExceptionTypes.Contains(exceptionType) && 
            !_config.RetryableExceptionTypes.Any(t => t.IsAssignableFrom(exceptionType)))
        {
            _logger.LogDebug("异常类型 {ExceptionType} 不在可重试列表中", exceptionType.Name);
            return false;
        }

        // 特殊处理 ClientResultException（OpenAI SDK）
        if (exception is ClientResultException clientEx)
        {
            if (_config.NonRetryableHttpStatusCodes.Contains(clientEx.Status))
            {
                _logger.LogDebug("HTTP状态码 {StatusCode} 不可重试", clientEx.Status);
                return false;
            }

            if (_config.RetryableHttpStatusCodes.Contains(clientEx.Status))
            {
                _logger.LogDebug("HTTP状态码 {StatusCode} 可重试", clientEx.Status);
                return true;
            }

            // 对于未明确定义的状态码，5xx 错误重试，4xx 错误不重试
            if (clientEx.Status >= 500 && clientEx.Status < 600)
            {
                _logger.LogDebug("5xx HTTP状态码 {StatusCode} 默认可重试", clientEx.Status);
                return true;
            }

            if (clientEx.Status >= 400 && clientEx.Status < 500)
            {
                _logger.LogDebug("4xx HTTP状态码 {StatusCode} 默认不可重试", clientEx.Status);
                return false;
            }
        }

        // HttpRequestException 特殊处理
        if (exception is HttpRequestException httpEx)
        {
            var statusCode = ExtractStatusCodeFromHttpException(httpEx);
            if (statusCode.HasValue)
            {
                return ShouldRetryBasedOnStatusCode(statusCode.Value);
            }
        }

        // 超时相关异常通常可以重试
        if (exception is TaskCanceledException or TimeoutException or OperationCanceledException)
        {
            _logger.LogDebug("超时相关异常可重试: {ExceptionType}", exceptionType.Name);
            return true;
        }

        _logger.LogDebug("未知异常类型 {ExceptionType}，默认不重试", exceptionType.Name);
        return false;
    }

    public TimeSpan CalculateDelay(int attemptNumber, Exception? lastException = null)
    {
        // 基础延迟时间：指数退避
        var baseDelay = TimeSpan.FromMilliseconds(
            _config.InitialDelay.TotalMilliseconds * Math.Pow(_config.BackoffMultiplier, attemptNumber));

        // 限制最大延迟
        if (baseDelay > _config.MaxDelay)
        {
            baseDelay = _config.MaxDelay;
        }

        // 针对特定异常调整延迟
        if (lastException is ClientResultException clientEx && clientEx.Status == 429)
        {
            // 429 错误（速率限制）使用更长的延迟
            baseDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * 2);
        }

        // 添加随机抖动避免惊群效应
        if (_config.EnableJitter)
        {
            var jitterFactor = 1.0 + (_random.NextDouble() * 0.5 - 0.25); // ±25% 抖动
            baseDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitterFactor);
        }

        _logger.LogDebug("计算延迟时间: {Delay} (尝试次数: {Attempt})", baseDelay, attemptNumber + 1);
        return baseDelay;
    }

    private bool ShouldRetryBasedOnStatusCode(int statusCode)
    {
        if (_config.NonRetryableHttpStatusCodes.Contains(statusCode))
        {
            return false;
        }

        if (_config.RetryableHttpStatusCodes.Contains(statusCode))
        {
            return true;
        }

        // 默认规则：5xx 重试，4xx 不重试
        return statusCode >= 500 && statusCode < 600;
    }

    private int? ExtractStatusCodeFromHttpException(HttpRequestException httpException)
    {
        // 尝试从异常消息中提取状态码
        var message = httpException.Message;
        var statusCodePattern = @"\b(\d{3})\b";
        var match = System.Text.RegularExpressions.Regex.Match(message, statusCodePattern);
        
        if (match.Success && int.TryParse(match.Groups[1].Value, out var statusCode))
        {
            return statusCode;
        }

        return null;
    }
}

/// <summary>
/// 重试策略工厂
/// </summary>
public static class RetryPolicyFactory
{
    /// <summary>
    /// 创建默认的API调用重试策略
    /// </summary>
    public static IRetryPolicy CreateApiRetryPolicy(ILogger<IntelligentRetryPolicy> logger)
    {
        var config = new RetryPolicyConfig
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0,
            EnableJitter = true
        };

        return new IntelligentRetryPolicy(config, logger);
    }

    /// <summary>
    /// 创建激进的重试策略（更多重试次数，适用于关键操作）
    /// </summary>
    public static IRetryPolicy CreateAggressiveRetryPolicy(ILogger<IntelligentRetryPolicy> logger)
    {
        var config = new RetryPolicyConfig
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromMinutes(1),
            BackoffMultiplier = 2.0,
            EnableJitter = true
        };

        return new IntelligentRetryPolicy(config, logger);
    }

    /// <summary>
    /// 创建保守的重试策略（更少重试次数，适用于快速失败场景）
    /// </summary>
    public static IRetryPolicy CreateConservativeRetryPolicy(ILogger<IntelligentRetryPolicy> logger)
    {
        var config = new RetryPolicyConfig
        {
            MaxRetries = 1,
            InitialDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
            EnableJitter = false
        };

        return new IntelligentRetryPolicy(config, logger);
    }
}