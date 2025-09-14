using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrchestrationApi.Configuration;
using OrchestrationApi.Middleware;
using OrchestrationApi.Services.Core;
using OrchestrationApi.Services.Providers;
using Serilog;
using SqlSugar;

using System.Text;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加服务
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // 支持Newtonsoft.Json

// 添加内存缓存
builder.Services.AddMemoryCache();

// 添加健康检查
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// 配置Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrchestrationApi",
        Version = "v1",
        Description = "多服务商AI API代理服务"
    });

    // 添加JWT认证配置
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 配置CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 配置数据库
DatabaseConfiguration.ConfigureDatabase(builder.Services, builder.Configuration);

// 配置请求日志选项
builder.Services.Configure<RequestLoggingOptions>(
    builder.Configuration.GetSection("OrchestrationApi:RequestLogging"));

// 注册服务
builder.Services.AddScoped<IRequestLogger, RequestLogger>();
builder.Services.AddScoped<IKeyManager, KeyManager>();
builder.Services.AddScoped<IProviderRouter, ProviderRouter>();
builder.Services.AddScoped<IProviderFactory, ProviderFactory>();
builder.Services.AddScoped<IProxyHttpClientService, ProxyHttpClientService>();
builder.Services.AddScoped<IVersionService, VersionService>();
builder.Services.AddHttpClient();

// 注册具体的服务商
builder.Services.AddScoped<OpenAiProvider>(provider =>
{
    var proxyHttpClientService = provider.GetRequiredService<IProxyHttpClientService>();
    var logger = provider.GetRequiredService<ILogger<OpenAiProvider>>();
    return new OpenAiProvider(proxyHttpClientService, logger);
});

builder.Services.AddScoped<AnthropicProvider>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var logger = provider.GetRequiredService<ILogger<AnthropicProvider>>();
    var proxyHttpClientService = provider.GetRequiredService<IProxyHttpClientService>();
    return new AnthropicProvider(logger, httpClient, proxyHttpClientService);
});

builder.Services.AddScoped<GeminiProvider>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var logger = provider.GetRequiredService<ILogger<GeminiProvider>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new GeminiProvider(httpClient, logger, configuration);
});

builder.Services.AddScoped<IMultiProviderService, MultiProviderService>();

// 注册后台服务
builder.Services.AddHostedService<OrchestrationApi.Services.Background.KeyHealthCheckService>();
builder.Services.AddHostedService<OrchestrationApi.Services.Background.LogCleanupService>();

// 添加认证
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration.GetValue<string>("OrchestrationApi:Auth:JwtSecret") ?? throw new InvalidOperationException("JWT密钥未配置"))),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // 配置JWT Bearer从多个位置获取token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 首先尝试从Authorization header获取token（默认行为）
                var token = context.Request.Headers["Authorization"]
                    .FirstOrDefault()?.Replace("Bearer ", "");

                // 如果header中没有token，则从cookie获取
                if (string.IsNullOrEmpty(token))
                {
                    token = context.Request.Cookies["authToken"];
                }

                // 设置token到context中
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

// 添加授权
builder.Services.AddAuthorization();

var app = builder.Build();

// 配置请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrchestrationApi v1");
        c.RoutePrefix = "swagger";
    });
}

// 中间件顺序很重要
app.UseCors("AllowAll");

// 自定义认证中间件（用于保护静态页面）
app.UseCustomAuthentication();

app.UseAuthentication();
app.UseAuthorization();

// 静态文件服务
app.UseStaticFiles();

// 健康检查端点
app.MapHealthChecks("/health");

// 路由
app.MapControllers();

// 默认路由到仪表板
app.MapGet("/", context =>
{
    context.Response.Redirect("/dashboard");
    return Task.CompletedTask;
});

// 仪表板路由
app.MapGet("/dashboard", async context =>
{
    await context.Response.SendFileAsync("wwwroot/dashboard.html");
});

// 日志查看器路由
app.MapGet("/logs", async context =>
{
    await context.Response.SendFileAsync("wwwroot/logs.html");
});

// 登录页面路由
app.MapGet("/login", async context =>
{
    await context.Response.SendFileAsync("wwwroot/login.html");
});

// 初始化数据库
try
{
    using var scope = app.Services.CreateScope();
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await dbInitializer.InitializeAsync();

    Log.Information("数据库初始化完成");
}
catch (Exception ex)
{
    Log.Fatal(ex, "数据库初始化失败，应用程序将退出");
    return;
}

// 启动应用
try
{
    var host = builder.Configuration.GetValue<string>("OrchestrationApi:Server:Host", "0.0.0.0");
    var port = builder.Configuration.GetValue<int>("OrchestrationApi:Server:Port", 5000);

    Log.Information("正在启动 OrchestrationApi 服务...");
    Log.Information("监听地址: http://{Host}:{Port}", host, port);
    Log.Information("环境: {Environment}", app.Environment.EnvironmentName);

    app.Urls.Add($"http://{host}:{port}");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// 数据库健康检查
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ISqlSugarClient _db;

    public DatabaseHealthCheck(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 简单的数据库连接测试
            await _db.Queryable<OrchestrationApi.Models.GroupConfig>().Take(1).ToListAsync();
            return HealthCheckResult.Healthy("数据库连接正常");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("数据库连接失败", ex);
        }
    }
}