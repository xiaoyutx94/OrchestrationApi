using SqlSugar;
using OrchestrationApi.Models;
using System.Text.Json;

namespace OrchestrationApi.Configuration;

/// <summary>
/// 数据库配置类
/// </summary>
public class DatabaseConfiguration
{
    /// <summary>
    /// 配置SqlSugar数据库连接
    /// </summary>
    public static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection("OrchestrationApi:Database");
        var dbType = dbConfig.GetValue<string>("Type", "sqlite");
        var connectionString = GetConnectionString(dbConfig, dbType);

        // 配置SqlSugar
        services.AddScoped<ISqlSugarClient>(provider =>
        {
            var config = new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = GetDbType(dbType),
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            };

            // 开发环境下启用SQL日志
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                config.MoreSettings = new ConnMoreSettings()
                {
                    IsAutoRemoveDataCache = true
                };
            }

            return new SqlSugarClient(config);
        });

        // 注册数据库初始化服务
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    private static string GetConnectionString(IConfigurationSection dbConfig, string dbType)
    {
        return dbType.ToLower() switch
        {
            "mysql" => dbConfig.GetValue<string>("MySqlConnectionString") ?? 
                      throw new InvalidOperationException("MySQL连接字符串未配置"),
            "sqlite" => dbConfig.GetValue<string>("ConnectionString") ?? 
                       "Data Source=Data/orchestration.db",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };
    }

    /// <summary>
    /// 获取数据库类型
    /// </summary>
    private static DbType GetDbType(string dbType)
    {
        return dbType.ToLower() switch
        {
            "mysql" => DbType.MySql,
            "sqlite" => DbType.Sqlite,
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };
    }
}

/// <summary>
/// 数据库初始化接口
/// </summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync();
    Task SeedDataAsync();
}

/// <summary>
/// 数据库初始化实现
/// </summary>
public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(ISqlSugarClient db, ILogger<DatabaseInitializer> logger, 
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 初始化数据库结构
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("开始初始化数据库结构...");

            // 确保数据目录存在 - 兼容本地开发和容器环境
            var dataDir = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
                ? Path.Combine(Directory.GetCurrentDirectory(), "Data")
                : "/app/data";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                _logger.LogInformation("创建数据目录: {DataDir}", dataDir);
            }

            // 初始化数据库版本管理表
            await InitializeDatabaseVersionTable();

            // 获取当前数据库版本
            var currentVersion = await GetCurrentDatabaseVersion();
            _logger.LogInformation("当前数据库版本: {Version}", currentVersion);

            // 创建表结构 - 分别处理每个表以便更好地处理错误
            try
            {
                _db.CodeFirst.InitTables(typeof(User));
                _logger.LogDebug("User表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "User表创建失败，可能已存在");
            }

            try
            {
                _db.CodeFirst.InitTables(typeof(UserSession));
                _logger.LogDebug("UserSession表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UserSession表创建失败，可能已存在");
            }

            try
            {
                _db.CodeFirst.InitTables(typeof(ProxyKey));
                _logger.LogDebug("ProxyKey表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ProxyKey表创建失败，可能已存在");
            }

            try
            {
                // 尝试手动创建GroupConfig表
                await CreateGroupConfigTableManually();
                _logger.LogDebug("GroupConfig表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GroupConfig表创建失败，尝试跳过");
            }

            try
            {
                // 尝试手动创建KeyValidation表
                await CreateKeyValidationTableManually();
                _logger.LogDebug("KeyValidation表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KeyValidation表创建失败，尝试跳过");
            }

            try
            {
                // 尝试手动创建RequestLog表
                await CreateRequestLogTableManually();
                _logger.LogDebug("RequestLog表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RequestLog表创建失败，尝试跳过");
            }

            try
            {
                // 尝试手动创建KeyUsageStats表
                await CreateKeyUsageStatsTableManually();
                _logger.LogDebug("KeyUsageStats表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KeyUsageStats表创建失败，尝试跳过");
            }

            // 执行数据库增量更新
            await ExecuteDatabaseMigrations(currentVersion);

            _logger.LogInformation("数据库结构初始化完成");

            // 初始化种子数据
            await SeedDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败");
            throw;
        }
    }

    /// <summary>
    /// 初始化种子数据
    /// </summary>
    public async Task SeedDataAsync()
    {
        try
        {
            _logger.LogInformation("开始初始化种子数据...");

            // 检查是否已有管理员用户
            var adminUser = await _db.Queryable<User>()
                .Where(u => u.Username == "admin")
                .FirstAsync();

            if (adminUser == null)
            {
                var authConfig = _configuration.GetSection("OrchestrationApi:Auth");
                var password = authConfig.GetValue<string>("Password", "admin123");
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                var newAdmin = new User
                {
                    Username = "admin",
                    PasswordHash = passwordHash,
                    Role = "Admin",
                    Enabled = true,
                    CreatedAt = DateTime.Now,
                    LastLoginAt = null
                };

                await _db.Insertable(newAdmin).ExecuteCommandAsync();
                _logger.LogInformation("创建默认管理员用户: admin");
            }
            else
            {
                // 更新现有用户的角色为大写的 Admin
                if (adminUser.Role != "Admin")
                {
                    adminUser.Role = "Admin";
                    await _db.Updateable(adminUser).ExecuteCommandAsync();
                    _logger.LogInformation("更新现有管理员用户角色: admin -> Admin");
                }
            }

            // 创建示例分组配置
            var existingGroups = await _db.Queryable<GroupConfig>().CountAsync();
            if (existingGroups == 0)
            {
                var sampleGroup = new GroupConfig
                {
                    Id = "default-openai",
                    GroupName = "Default OpenAI",
                    ProviderType = "openai",
                    ApiKeys = "[\"sk-your-openai-key-here\"]",
                    Models = "[\"gpt-3.5-turbo\", \"gpt-4\", \"gpt-4-turbo\"]",
                    ModelAliases = "{}",
                    ParameterOverrides = "{}",
                    Headers = "{}",
                    BalancePolicy = "round_robin",
                    RetryCount = 3,
                    Timeout = 60,
                    RpmLimit = 100,
                    Priority = 1,
                    Enabled = false, // 默认禁用，需要用户配置有效密钥后启用
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _db.Insertable(sampleGroup).ExecuteCommandAsync();
                _logger.LogInformation("创建示例分组配置: {GroupName}", sampleGroup.GroupName);

                // 为示例分组创建密钥使用统计数据
                try
                {
                    await InitializeKeyUsageStats(sampleGroup.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "初始化密钥使用统计失败");
                }
            }

            _logger.LogInformation("种子数据初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "种子数据初始化失败");
            throw;
        }
    }

    /// <summary>
    /// 手动创建GroupConfig表
    /// </summary>
    private async Task CreateGroupConfigTableManually()
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string createSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE TABLE IF NOT EXISTS orch_groups (
                    id TEXT PRIMARY KEY,
                    group_name TEXT NOT NULL,
                    provider_type TEXT NOT NULL,
                    base_url TEXT,
                    api_keys TEXT,
                    models TEXT,
                    model_aliases TEXT,
                    parameter_overrides TEXT,
                    headers TEXT,
                    balance_policy TEXT DEFAULT 'round_robin',
                    retry_count INTEGER DEFAULT 3,
                    timeout INTEGER DEFAULT 60,
                    rpm_limit INTEGER DEFAULT 0,
                    test_model TEXT,
                    priority INTEGER DEFAULT 0,
                    enabled INTEGER DEFAULT 1,
                    proxy_enabled INTEGER DEFAULT 0,
                    proxy_config TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_deleted INTEGER DEFAULT 0
                )",
            DbType.MySql => @"
                CREATE TABLE IF NOT EXISTS orch_groups (
                    id VARCHAR(100) PRIMARY KEY,
                    group_name VARCHAR(100) NOT NULL,
                    provider_type VARCHAR(50) NOT NULL,
                    base_url VARCHAR(500),
                    api_keys TEXT,
                    models TEXT,
                    model_aliases TEXT,
                    parameter_overrides TEXT,
                    headers TEXT,
                    balance_policy VARCHAR(50) DEFAULT 'round_robin',
                    retry_count INT DEFAULT 3,
                    timeout INT DEFAULT 60,
                    rpm_limit INT DEFAULT 0,
                    test_model VARCHAR(200),
                    priority INT DEFAULT 0,
                    enabled TINYINT DEFAULT 1,
                    proxy_enabled TINYINT DEFAULT 0,
                    proxy_config TEXT,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL,
                    is_deleted TINYINT DEFAULT 0
                )",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        await _db.Ado.ExecuteCommandAsync(createSql);
    }

    /// <summary>
    /// 手动创建KeyValidation表
    /// </summary>
    private async Task CreateKeyValidationTableManually()
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string createSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE TABLE IF NOT EXISTS orch_key_validation (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    group_id TEXT,
                    api_key_hash TEXT NOT NULL,
                    provider_type TEXT,
                    is_valid INTEGER DEFAULT 0,
                    error_count INTEGER DEFAULT 0,
                    last_error TEXT,
                    last_status_code INTEGER,
                    last_validated_at TEXT,
                    created_at TEXT NOT NULL
                )",
            DbType.MySql => @"
                CREATE TABLE IF NOT EXISTS orch_key_validation (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    group_id VARCHAR(100),
                    api_key_hash VARCHAR(64) NOT NULL,
                    provider_type VARCHAR(50),
                    is_valid TINYINT DEFAULT 0,
                    error_count INT DEFAULT 0,
                    last_error TEXT,
                    last_status_code INT,
                    last_validated_at DATETIME,
                    created_at DATETIME NOT NULL
                )",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        await _db.Ado.ExecuteCommandAsync(createSql);
    }

    /// <summary>
    /// 手动创建RequestLog表
    /// </summary>
    private async Task CreateRequestLogTableManually()
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string createSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE TABLE IF NOT EXISTS orch_request_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    request_id TEXT NOT NULL,
                    proxy_key_id INTEGER,
                    group_id TEXT,
                    provider_type TEXT,
                    model TEXT,
                    method TEXT NOT NULL,
                    endpoint TEXT NOT NULL,
                    request_body TEXT,
                    response_body TEXT,
                    request_headers TEXT,
                    response_headers TEXT,
                    content_truncated INTEGER DEFAULT 0,
                    status_code INTEGER NOT NULL,
                    duration_ms INTEGER NOT NULL,
                    prompt_tokens INTEGER,
                    completion_tokens INTEGER,
                    total_tokens INTEGER,
                    error_message TEXT,
                    client_ip TEXT,
                    user_agent TEXT,
                    openrouter_key TEXT,
                    has_tools INTEGER DEFAULT 0,
                    is_streaming INTEGER DEFAULT 0,
                    created_at TEXT NOT NULL
                )",
            DbType.MySql => @"
                CREATE TABLE IF NOT EXISTS orch_request_logs (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    request_id VARCHAR(50) NOT NULL,
                    proxy_key_id INT,
                    group_id VARCHAR(100),
                    provider_type VARCHAR(50),
                    model VARCHAR(100),
                    method VARCHAR(10) NOT NULL,
                    endpoint VARCHAR(200) NOT NULL,
                    request_body TEXT,
                    response_body TEXT,
                    request_headers TEXT,
                    response_headers TEXT,
                    content_truncated TINYINT DEFAULT 0,
                    status_code INT NOT NULL,
                    duration_ms BIGINT NOT NULL,
                    prompt_tokens INT,
                    completion_tokens INT,
                    total_tokens INT,
                    error_message TEXT,
                    client_ip VARCHAR(45),
                    user_agent VARCHAR(500),
                    openrouter_key VARCHAR(100),
                    has_tools TINYINT DEFAULT 0,
                    is_streaming TINYINT DEFAULT 0,
                    created_at DATETIME NOT NULL
                )",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        await _db.Ado.ExecuteCommandAsync(createSql);
    }

    /// <summary>
    /// 手动创建KeyUsageStats表
    /// </summary>
    private async Task CreateKeyUsageStatsTableManually()
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string createSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE TABLE IF NOT EXISTS orch_key_usage_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    group_id TEXT NOT NULL,
                    api_key_hash TEXT NOT NULL,
                    usage_count INTEGER DEFAULT 0,
                    last_used_at TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    UNIQUE(group_id, api_key_hash)
                )",
            DbType.MySql => @"
                CREATE TABLE IF NOT EXISTS orch_key_usage_stats (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    group_id VARCHAR(100) NOT NULL,
                    api_key_hash VARCHAR(64) NOT NULL,
                    usage_count BIGINT DEFAULT 0,
                    last_used_at DATETIME,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL,
                    UNIQUE KEY uk_group_key (group_id, api_key_hash)
                )",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        await _db.Ado.ExecuteCommandAsync(createSql);

        // 创建索引以提高查询性能
        string createIndexSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE INDEX IF NOT EXISTS idx_key_usage_stats_group_key 
                ON orch_key_usage_stats(group_id, api_key_hash)",
            DbType.MySql => @"
                CREATE INDEX IF NOT EXISTS idx_key_usage_stats_group_key 
                ON orch_key_usage_stats(group_id, api_key_hash)",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        try
        {
            await _db.Ado.ExecuteCommandAsync(createIndexSql);
            _logger.LogDebug("KeyUsageStats索引创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KeyUsageStats索引创建失败，可能已存在");
        }
    }

    /// <summary>
    /// 初始化密钥使用统计数据
    /// </summary>
    private async Task InitializeKeyUsageStats(string groupId)
    {
        try
        {
            // 获取分组配置
            var group = await _db.Queryable<GroupConfig>()
                .Where(g => g.Id == groupId)
                .FirstAsync();

            if (group == null)
            {
                _logger.LogWarning("分组 {GroupId} 不存在，跳过密钥使用统计初始化", groupId);
                return;
            }

            // 解析API密钥
            var apiKeysJson = group.ApiKeys ?? "[]";
            var apiKeys = System.Text.Json.JsonSerializer.Deserialize<string[]>(apiKeysJson) ?? Array.Empty<string>();

            if (!apiKeys.Any())
            {
                _logger.LogDebug("分组 {GroupId} 没有API密钥，跳过使用统计初始化", groupId);
                return;
            }

            // 为每个密钥创建初始统计记录
            foreach (var apiKey in apiKeys)
            {
                var keyHash = ComputeKeyHash(apiKey);
                
                // 检查是否已存在统计记录
                var existingStats = await _db.Queryable<KeyUsageStats>()
                    .Where(s => s.GroupId == groupId && s.ApiKeyHash == keyHash)
                    .FirstAsync();

                if (existingStats == null)
                {
                    var newStats = new KeyUsageStats
                    {
                        GroupId = groupId,
                        ApiKeyHash = keyHash,
                        UsageCount = 0,
                        LastUsedAt = null,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await _db.Insertable(newStats).ExecuteCommandAsync();
                    _logger.LogDebug("为分组 {GroupId} 创建密钥使用统计记录，密钥: {KeyPrefix}", 
                        groupId, apiKey.Substring(0, Math.Min(8, apiKey.Length)));
                }
            }

            _logger.LogInformation("分组 {GroupId} 的密钥使用统计初始化完成，共处理 {KeyCount} 个密钥", 
                groupId, apiKeys.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化分组 {GroupId} 密钥使用统计时发生异常", groupId);
            throw;
        }
    }

    /// <summary>
    /// 计算密钥哈希值
    /// </summary>
    private static string ComputeKeyHash(string apiKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }

    #region 数据库版本管理和增量更新

    /// <summary>
    /// 当前数据库版本
    /// </summary>
    private const string CURRENT_DATABASE_VERSION = "1.4.0";

    /// <summary>
    /// 初始化数据库版本管理表
    /// </summary>
    private async Task InitializeDatabaseVersionTable()
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string createSql = dbType switch
        {
            DbType.Sqlite => @"
                CREATE TABLE IF NOT EXISTS orch_database_versions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    version TEXT NOT NULL UNIQUE,
                    applied_at TEXT NOT NULL,
                    description TEXT
                )",
            DbType.MySql => @"
                CREATE TABLE IF NOT EXISTS orch_database_versions (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    version VARCHAR(20) NOT NULL UNIQUE,
                    applied_at DATETIME NOT NULL,
                    description TEXT
                )",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        await _db.Ado.ExecuteCommandAsync(createSql);
        _logger.LogDebug("数据库版本管理表创建成功");
    }

    /// <summary>
    /// 获取当前数据库版本
    /// </summary>
    private async Task<string> GetCurrentDatabaseVersion()
    {
        try
        {
            var sql = "SELECT version FROM orch_database_versions ORDER BY applied_at DESC LIMIT 1";
            var version = await _db.Ado.GetStringAsync(sql);
            return string.IsNullOrEmpty(version) ? "0.0.0" : version;
        }
        catch
        {
            // 如果版本表不存在或查询失败，返回初始版本
            return "0.0.0";
        }
    }

    /// <summary>
    /// 记录数据库版本更新
    /// </summary>
    private async Task RecordDatabaseVersion(string version, string description)
    {
        var dbType = _db.CurrentConnectionConfig.DbType;
        
        string insertSql = dbType switch
        {
            DbType.Sqlite => "INSERT OR IGNORE INTO orch_database_versions (version, applied_at, description) VALUES (@version, @applied_at, @description)",
            DbType.MySql => "INSERT IGNORE INTO orch_database_versions (version, applied_at, description) VALUES (@version, @applied_at, @description)",
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };

        object appliedAt = dbType == DbType.Sqlite 
            ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") 
            : DateTime.Now;
        
        await _db.Ado.ExecuteCommandAsync(insertSql, new { version, applied_at = appliedAt, description });
        _logger.LogInformation("记录数据库版本: {Version} - {Description}", version, description);
    }

    /// <summary>
    /// 检查列是否存在
    /// </summary>
    private async Task<bool> ColumnExists(string tableName, string columnName)
    {
        try
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string sql = dbType switch
            {
                DbType.Sqlite => @"
                    SELECT COUNT(*) FROM pragma_table_info(@tableName) 
                    WHERE name = @columnName",
                DbType.MySql => @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            var count = await _db.Ado.GetIntAsync(sql, new { tableName, columnName });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查列 {TableName}.{ColumnName} 是否存在时出错", tableName, columnName);
            return false;
        }
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    private async Task<bool> TableExists(string tableName)
    {
        try
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string sql = dbType switch
            {
                DbType.Sqlite => @"
                    SELECT COUNT(*) FROM sqlite_master 
                    WHERE type='table' AND name=@tableName",
                DbType.MySql => @"
                    SELECT COUNT(*) FROM information_schema.TABLES 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            var count = await _db.Ado.GetIntAsync(sql, new { tableName });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查表 {TableName} 是否存在时出错", tableName);
            return false;
        }
    }

    /// <summary>
    /// 执行数据库增量更新
    /// </summary>
    private async Task ExecuteDatabaseMigrations(string currentVersion)
    {
        _logger.LogInformation("开始执行数据库增量更新，当前版本: {CurrentVersion}, 目标版本: {TargetVersion}", 
            currentVersion, CURRENT_DATABASE_VERSION);

        var migrations = GetMigrationScripts();
        
        foreach (var migration in migrations)
        {
            if (IsVersionGreater(migration.Version, currentVersion))
            {
                try
                {
                    _logger.LogInformation("执行数据库迁移: {Version} - {Description}", 
                        migration.Version, migration.Description);
                    
                    await migration.ExecuteAsync(_db, _logger, this);
                    await RecordDatabaseVersion(migration.Version, migration.Description);
                    
                    _logger.LogInformation("数据库迁移完成: {Version}", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "数据库迁移失败: {Version} - {Description}", 
                        migration.Version, migration.Description);
                    throw;
                }
            }
        }

        _logger.LogInformation("数据库增量更新完成");
    }

    /// <summary>
    /// 比较版本号大小
    /// </summary>
    private static bool IsVersionGreater(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();
        
        var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
        
        for (int i = 0; i < maxLength; i++)
        {
            var v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2Part = i < v2Parts.Length ? v2Parts[i] : 0;
            
            if (v1Part > v2Part) return true;
            if (v1Part < v2Part) return false;
        }
        
        return false;
    }

    /// <summary>
    /// 获取所有迁移脚本
    /// 注意：新增迁移时请遵循以下规则：
    /// 1. 版本号采用语义化版本控制 (major.minor.patch)
    /// 2. 版本号必须递增，不能重复
    /// 3. 每个迁移都应该是幂等的（可重复执行）
    /// 4. 迁移应该向后兼容，不要删除已有数据
    /// 5. 复杂迁移应该拆分成多个小步骤
    /// </summary>
    private List<DatabaseMigration> GetMigrationScripts()
    {
        return new List<DatabaseMigration>
        {
            new DatabaseMigration
            {
                Version = "1.1.0",
                Description = "添加 KeyValidation 表的 last_status_code 字段",
                ExecuteAsync = async (db, logger, initializer) =>
                {
                    await initializer.AddLastStatusCodeToKeyValidation();
                }
            },
            new DatabaseMigration
            {
                Version = "1.2.0", 
                Description = "添加 GroupConfig 表的 is_deleted 字段",
                ExecuteAsync = async (db, logger, initializer) =>
                {
                    await initializer.AddIsDeletedToGroupConfig();
                }
            },
            new DatabaseMigration
            {
                Version = "1.3.0",
                Description = "添加 GroupConfig 表的 headers 字段",
                ExecuteAsync = async (db, logger, initializer) =>
                {
                    await initializer.AddHeadersToGroupConfig();
                }
            },
            new DatabaseMigration
            {
                Version = "1.4.0",
                Description = "添加 GroupConfig 表的代理配置字段 (proxy_enabled, proxy_config)",
                ExecuteAsync = async (db, logger, initializer) =>
                {
                    await initializer.AddProxyConfigToGroupConfig();
                }
            }
            
            // 添加新迁移的示例：
            // new DatabaseMigration
            // {
            //     Version = "1.3.0",
            //     Description = "添加新表或字段的描述",
            //     ExecuteAsync = async (db, logger, initializer) =>
            //     {
            //         // 在这里调用具体的迁移方法
            //         await initializer.YourNewMigrationMethod();
            //     }
            // }
        };
    }

    /// <summary>
    /// 添加 last_status_code 字段到 KeyValidation 表
    /// </summary>
    private async Task AddLastStatusCodeToKeyValidation()
    {
        const string tableName = "orch_key_validation";
        const string columnName = "last_status_code";
        
        if (await TableExists(tableName) && !await ColumnExists(tableName, columnName))
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string alterSql = dbType switch
            {
                DbType.Sqlite => $"ALTER TABLE {tableName} ADD COLUMN {columnName} INTEGER",
                DbType.MySql => $"ALTER TABLE {tableName} ADD COLUMN {columnName} INT",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            await _db.Ado.ExecuteCommandAsync(alterSql);
            _logger.LogInformation("成功添加字段 {TableName}.{ColumnName}", tableName, columnName);
        }
        else
        {
            _logger.LogDebug("字段 {TableName}.{ColumnName} 已存在，跳过添加", tableName, columnName);
        }
    }

    /// <summary>
    /// 添加 is_deleted 字段到 GroupConfig 表
    /// </summary>
    private async Task AddIsDeletedToGroupConfig()
    {
        const string tableName = "orch_groups";
        const string columnName = "is_deleted";
        
        if (await TableExists(tableName) && !await ColumnExists(tableName, columnName))
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string alterSql = dbType switch
            {
                DbType.Sqlite => $"ALTER TABLE {tableName} ADD COLUMN {columnName} INTEGER DEFAULT 0",
                DbType.MySql => $"ALTER TABLE {tableName} ADD COLUMN {columnName} TINYINT DEFAULT 0",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            await _db.Ado.ExecuteCommandAsync(alterSql);
            _logger.LogInformation("成功添加字段 {TableName}.{ColumnName}", tableName, columnName);
        }
        else
        {
            _logger.LogDebug("字段 {TableName}.{ColumnName} 已存在，跳过添加", tableName, columnName);
        }
    }

    /// <summary>
    /// 添加 headers 字段到 GroupConfig 表
    /// </summary>
    private async Task AddHeadersToGroupConfig()
    {
        const string tableName = "orch_groups";
        const string columnName = "headers";
        
        if (await TableExists(tableName) && !await ColumnExists(tableName, columnName))
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string alterSql = dbType switch
            {
                DbType.Sqlite => $"ALTER TABLE {tableName} ADD COLUMN {columnName} TEXT",
                DbType.MySql => $"ALTER TABLE {tableName} ADD COLUMN {columnName} TEXT",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            await _db.Ado.ExecuteCommandAsync(alterSql);
            _logger.LogInformation("成功添加字段 {TableName}.{ColumnName}", tableName, columnName);
        }
        else
        {
            _logger.LogDebug("字段 {TableName}.{ColumnName} 已存在，跳过添加", tableName, columnName);
        }
    }

    /// <summary>
    /// 添加代理配置字段到 GroupConfig 表
    /// </summary>
    private async Task AddProxyConfigToGroupConfig()
    {
        const string tableName = "orch_groups";
        
        // 添加 proxy_enabled 字段
        const string proxyEnabledColumn = "proxy_enabled";
        if (await TableExists(tableName) && !await ColumnExists(tableName, proxyEnabledColumn))
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string alterSql = dbType switch
            {
                DbType.Sqlite => $"ALTER TABLE {tableName} ADD COLUMN {proxyEnabledColumn} INTEGER DEFAULT 0",
                DbType.MySql => $"ALTER TABLE {tableName} ADD COLUMN {proxyEnabledColumn} TINYINT DEFAULT 0",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            await _db.Ado.ExecuteCommandAsync(alterSql);
            _logger.LogInformation("成功添加字段 {TableName}.{ColumnName}", tableName, proxyEnabledColumn);
        }
        else
        {
            _logger.LogDebug("字段 {TableName}.{ColumnName} 已存在，跳过添加", tableName, proxyEnabledColumn);
        }

        // 添加 proxy_config 字段
        const string proxyConfigColumn = "proxy_config";
        if (await TableExists(tableName) && !await ColumnExists(tableName, proxyConfigColumn))
        {
            var dbType = _db.CurrentConnectionConfig.DbType;
            
            string alterSql = dbType switch
            {
                DbType.Sqlite => $"ALTER TABLE {tableName} ADD COLUMN {proxyConfigColumn} TEXT",
                DbType.MySql => $"ALTER TABLE {tableName} ADD COLUMN {proxyConfigColumn} TEXT",
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
            };

            await _db.Ado.ExecuteCommandAsync(alterSql);
            _logger.LogInformation("成功添加字段 {TableName}.{ColumnName}", tableName, proxyConfigColumn);
        }
        else
        {
            _logger.LogDebug("字段 {TableName}.{ColumnName} 已存在，跳过添加", tableName, proxyConfigColumn);
        }
    }

    #endregion
}

/// <summary>
/// 数据库迁移脚本定义
/// </summary>
public class DatabaseMigration
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Func<ISqlSugarClient, ILogger<DatabaseInitializer>, DatabaseInitializer, Task> ExecuteAsync { get; set; } = null!;
}
