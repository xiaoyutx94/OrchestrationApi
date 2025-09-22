using Microsoft.IdentityModel.Tokens;
using OrchestrationApi.Models;
using SqlSugar;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace OrchestrationApi.Middleware;

/// <summary>
/// 身份验证中间件，用于保护静态页面
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    // 需要保护的路径
    private readonly HashSet<string> _protectedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/dashboard",
        "/logs",
        "/health-report",
        "/",
        "/dashboard.html",
        "/logs.html",
        "/health-report.html"
    };

    // 公共路径（不需要认证）
    private readonly HashSet<string> _publicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/login",
        "/login.html",
        "/auth/login",
        "/auth/verify",
        "/health",
        "/swagger"
    };

    public AuthenticationMiddleware(
        RequestDelegate next, 
        IConfiguration configuration,
        ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 检查是否是受保护的路径
        if (IsProtectedPath(path))
        {
            var isAuthenticated = await ValidateAuthenticationAsync(context);
            
            if (!isAuthenticated)
            {
                _logger.LogWarning("未授权访问受保护路径: {Path} from IP: {IP}", 
                    path, context.Connection.RemoteIpAddress);
                
                // 重定向到登录页面
                context.Response.Redirect("/login");
                return;
            }
        }

        await _next(context);
    }

    private bool IsProtectedPath(string path)
    {
        // 检查是否是公共路径
        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // 检查是否是API路径（以/api开头的路径通过JWT认证）
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 检查是否是admin API路径（通过JWT认证）
        if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 检查是否是auth API路径
        if (path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 静态资源文件
        if (path.Contains(".css") || path.Contains(".js") || path.Contains(".ico") || 
            path.Contains(".png") || path.Contains(".jpg") || path.Contains(".svg"))
        {
            return false;
        }

        // 检查是否明确需要保护
        return _protectedPaths.Contains(path) || path == "/" || 
               string.IsNullOrEmpty(path) || path == "/dashboard" || path == "/logs";
    }

    private async Task<bool> ValidateAuthenticationAsync(HttpContext context)
    {
        try
        {
            // 首先尝试从Cookie中获取token
            var token = context.Request.Cookies["authToken"];
            _logger.LogInformation("认证检查 - Cookie Token: {HasToken}", !string.IsNullOrEmpty(token));
            
            // 如果Cookie中没有，尝试从Authorization header获取
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    token = authHeader.Substring(7);
                    _logger.LogInformation("认证检查 - Header Token: 找到");
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("认证检查 - 未找到Token (Cookie或Header)");
                return false;
            }

            // 验证JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(
                _configuration.GetValue<string>("OrchestrationApi:Auth:JwtSecret") ?? 
                throw new InvalidOperationException("JWT密钥未配置"));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // 验证会话是否仍然有效
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

            var session = await db.Queryable<UserSession>()
                .Where(s => s.Token == token && s.ExpiresAt > DateTime.Now)
                .FirstAsync();

            if (session == null)
            {
                _logger.LogWarning("Token有效但会话不存在或已过期");
                return false;
            }

            // 更新最后访问时间
            await db.Updateable<UserSession>()
                .SetColumns(s => new UserSession { LastAccessedAt = DateTime.Now })
                .Where(s => s.Id == session.Id)
                .ExecuteCommandAsync();

            // 将用户信息添加到HttpContext中
            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = principal.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "";

            context.Items["UserId"] = userId;
            context.Items["Username"] = username;
            context.Items["Role"] = role;

            return true;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Token已过期");
            return false;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token验证失败");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "身份验证过程中发生异常");
            return false;
        }
    }
}

/// <summary>
/// 认证中间件扩展方法
/// </summary>
public static class AuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationMiddleware>();
    }
}
