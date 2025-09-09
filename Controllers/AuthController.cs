using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OrchestrationApi.Models;
using SqlSugar;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OrchestrationApi.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ISqlSugarClient _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ISqlSugarClient db,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(LoginResponse), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 查找用户
            var user = await _db.Queryable<User>()
                .Where(u => u.Username == request.Username && u.Enabled)
                .FirstAsync();

            if (user == null)
            {
                _logger.LogWarning("登录失败：用户不存在 - {Username}", request.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = "用户名或密码错误"
                });
            }

            // 验证密码
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("登录失败：密码错误 - {Username}", request.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = "用户名或密码错误"
                });
            }

            // 生成JWT Token
            var token = GenerateJwtToken(user);
            var expiresAt = DateTime.Now.AddSeconds(
                _configuration.GetValue<int>("OrchestrationApi:Auth:SessionTimeout", 86400));

            // 创建会话记录
            var session = new UserSession
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.Now,
                LastAccessedAt = DateTime.Now,
                IpAddress = GetClientIpAddress(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].FirstOrDefault()
            };

            await _db.Insertable(session).ExecuteCommandAsync();

            // 更新用户最后登录时间
            await _db.Updateable<User>()
                .SetColumns(u => new User { LastLoginAt = DateTime.Now })
                .Where(u => u.Id == user.Id)
                .ExecuteCommandAsync();

            _logger.LogInformation("用户登录成功 - {Username} (ID: {UserId})", user.Username, user.Id);

            // 设置认证Cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false, // 允许JavaScript访问，因为前端需要读取token
                Secure = false, // 开发环境不需要HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = expiresAt
            };
            
            HttpContext.Response.Cookies.Append("authToken", token, cookieOptions);

            return Ok(new LoginResponse
            {
                Success = true,
                Message = "登录成功",
                Token = token,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录处理时发生异常");
            return StatusCode(500, new LoginResponse
            {
                Success = false,
                Message = "服务器内部错误"
            });
        }
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var token = ExtractBearerToken(authHeader);

            if (!string.IsNullOrEmpty(token))
            {
                // 删除会话记录
                await _db.Deleteable<UserSession>()
                    .Where(s => s.Token == token)
                    .ExecuteCommandAsync();

                _logger.LogInformation("用户登出成功");
            }

            // 清除认证Cookie
            HttpContext.Response.Cookies.Delete("authToken");

            return Ok(new { message = "登出成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出处理时发生异常");
            return StatusCode(500, new { error = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 验证Token
    /// </summary>
    [HttpGet("verify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> VerifyToken()
    {
        try
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var token = ExtractBearerToken(authHeader);

            _logger.LogInformation("Verifying token: Authorization Header = {AuthHeader}, Token = {Token}", authHeader, token);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { valid = false, message = "Token不能为空" });
            }

            // 验证Token格式和签名
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

            // 检查会话是否存在且未过期
            var session = await _db.Queryable<UserSession>()
                .Where(s => s.Token == token && s.ExpiresAt > DateTime.Now)
                .FirstAsync();

            if (session == null)
            {
                return Unauthorized(new { valid = false, message = "会话已过期或不存在" });
            }

            // 更新最后访问时间
            await _db.Updateable<UserSession>()
                .SetColumns(s => new UserSession { LastAccessedAt = DateTime.Now })
                .Where(s => s.Id == session.Id)
                .ExecuteCommandAsync();

            // 获取用户信息
            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _db.Queryable<User>()
                .Where(u => u.Id == userId && u.Enabled)
                .FirstAsync();

            if (user == null)
            {
                return Unauthorized(new { valid = false, message = "用户不存在或已禁用" });
            }

            return Ok(new 
            { 
                valid = true, 
                user = new 
                { 
                    id = user.Id,
                    username = user.Username,
                    role = user.Role
                }
            });
        }
        catch (SecurityTokenExpiredException)
        {
            return Unauthorized(new { valid = false, message = "Token已过期" });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token验证失败");
            return Unauthorized(new { valid = false, message = "Token无效" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token验证时发生异常");
            return StatusCode(500, new { valid = false, message = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "请求参数无效" });
            }

            // 验证当前用户
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var token = ExtractBearerToken(authHeader);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, message = "未授权" });
            }

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

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var user = await _db.Queryable<User>()
                .Where(u => u.Id == userId && u.Enabled)
                .FirstAsync();

            if (user == null)
            {
                return Unauthorized(new { success = false, message = "用户不存在" });
            }

            // 验证当前密码
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { success = false, message = "当前密码错误" });
            }

            // 更新密码
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _db.Updateable<User>()
                .SetColumns(u => new User { PasswordHash = newPasswordHash })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync();

            _logger.LogInformation("用户修改密码成功 - {Username} (ID: {UserId})", user.Username, user.Id);

            return Ok(new { success = true, message = "密码修改成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码时发生异常");
            return StatusCode(500, new { success = false, message = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 更新用户信息（用户名和密码）
    /// </summary>
    [HttpPost("update-user")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, error = "请求参数无效" });
            }

            // 验证当前用户
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var token = ExtractBearerToken(authHeader);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, error = "未授权" });
            }

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

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var user = await _db.Queryable<User>()
                .Where(u => u.Id == userId && u.Enabled)
                .FirstAsync();

            if (user == null)
            {
                return Unauthorized(new { success = false, error = "用户不存在" });
            }

            // 验证当前密码
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { success = false, error = "当前密码错误" });
            }

            // 检查是否有要更新的内容
            if (string.IsNullOrEmpty(request.NewUsername) && string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { success = false, error = "请至少修改用户名或密码中的一项" });
            }

            // 构建更新对象
            var updateUser = new User();
            var hasUpdates = false;

            // 更新用户名
            if (!string.IsNullOrEmpty(request.NewUsername) && request.NewUsername != user.Username)
            {
                updateUser.Username = request.NewUsername;
                hasUpdates = true;
            }

            // 更新密码
            if (!string.IsNullOrEmpty(request.NewPassword))
            {
                updateUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                hasUpdates = true;
            }

            if (hasUpdates)
            {
                // 使用动态更新
                if (!string.IsNullOrEmpty(request.NewUsername) && !string.IsNullOrEmpty(request.NewPassword))
                {
                    // 同时更新用户名和密码
                    await _db.Updateable<User>()
                        .SetColumns(u => new User { 
                            Username = updateUser.Username, 
                            PasswordHash = updateUser.PasswordHash 
                        })
                        .Where(u => u.Id == userId)
                        .ExecuteCommandAsync();
                }
                else if (!string.IsNullOrEmpty(request.NewUsername))
                {
                    // 只更新用户名
                    await _db.Updateable<User>()
                        .SetColumns(u => new User { Username = updateUser.Username })
                        .Where(u => u.Id == userId)
                        .ExecuteCommandAsync();
                }
                else if (!string.IsNullOrEmpty(request.NewPassword))
                {
                    // 只更新密码
                    await _db.Updateable<User>()
                        .SetColumns(u => new User { PasswordHash = updateUser.PasswordHash })
                        .Where(u => u.Id == userId)
                        .ExecuteCommandAsync();
                }

                // 如果修改了密码，删除该用户的所有会话（强制重新登录）
                if (!string.IsNullOrEmpty(request.NewPassword))
                {
                    await _db.Deleteable<UserSession>()
                        .Where(s => s.UserId == userId)
                        .ExecuteCommandAsync();
                    
                    _logger.LogInformation("用户修改密码，已清除所有会话 - {Username} (ID: {UserId})", user.Username, user.Id);
                }
                
                _logger.LogInformation("用户更新信息成功 - {Username} (ID: {UserId})", user.Username, user.Id);
                
                return Ok(new { success = true, message = "用户信息更新成功" });
            }

            return BadRequest(new { success = false, error = "没有需要更新的内容" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户信息时发生异常");
            return StatusCode(500, new { success = false, error = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 生成JWT Token
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration.GetValue<string>("OrchestrationApi:Auth:JwtSecret") ?? 
                       throw new InvalidOperationException("JWT密钥未配置");

        var sessionTimeout = _configuration.GetValue<int>("OrchestrationApi:Auth:SessionTimeout", 86400);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.Now.AddSeconds(sessionTimeout),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 提取Bearer Token
    /// </summary>
    private static string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader))
            return null;

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader[7..];
        }

        return null;
    }

    /// <summary>
    /// 获取客户端IP地址
    /// </summary>
    private string? GetClientIpAddress()
    {
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// 修改密码请求
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
