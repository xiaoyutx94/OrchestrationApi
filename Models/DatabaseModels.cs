using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace OrchestrationApi.Models;

/// <summary>
/// 用户组配置表
/// </summary>
[SugarTable("orch_groups")]
public class GroupConfig
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = false, Length = 100)]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "group_name", Length = 100)]
    [Required]
    public string GroupName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "provider_type", Length = 50)]
    [Required]
    public string ProviderType { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "base_url", Length = 500)]
    public string BaseUrl { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "api_keys", ColumnDataType = "TEXT")]
    public string ApiKeys { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "models", ColumnDataType = "TEXT")]
    public string Models { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "model_aliases", ColumnDataType = "TEXT")]
    public string ModelAliases { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "parameter_overrides", ColumnDataType = "TEXT")]
    public string ParameterOverrides { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "headers", ColumnDataType = "TEXT")]
    public string Headers { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "balance_policy", Length = 50)]
    public string BalancePolicy { get; set; } = "round_robin";

    [SugarColumn(ColumnName = "retry_count")]
    public int RetryCount { get; set; } = 3;

    [SugarColumn(ColumnName = "timeout")]
    public int Timeout { get; set; } = 60;

    [SugarColumn(ColumnName = "rpm_limit")]
    [DefaultValue(0)]
    public int RpmLimit { get; set; } = 0;

    [SugarColumn(ColumnName = "test_model", Length = 200)]
    public string TestModel { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "priority")]
    [DefaultValue(0)]
    public int Priority { get; set; } = 0;

    [SugarColumn(ColumnName = "enabled")]
    public bool Enabled { get; set; } = true;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "is_deleted")]
    [DefaultValue(false)]
    public bool IsDeleted { get; set; } = false;

    [SugarColumn(ColumnName = "proxy_enabled")]
    [DefaultValue(false)]
    public bool ProxyEnabled { get; set; } = false;

    [SugarColumn(ColumnName = "proxy_config", ColumnDataType = "TEXT")]
    public string ProxyConfig { get; set; } = string.Empty;
}

/// <summary>
/// 代理密钥管理表
/// </summary>
[SugarTable("orch_proxy_keys")]
public class ProxyKey
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "key_name", Length = 100)]
    [Required]
    public string KeyName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "key_value", Length = 500)]
    [Required]
    public string KeyValue { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "description", Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "allowed_groups", ColumnDataType = "TEXT")]
    public string AllowedGroups { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "group_balance_policy", Length = 50)]
    public string GroupBalancePolicy { get; set; } = "failover";

    [SugarColumn(ColumnName = "group_weights", ColumnDataType = "TEXT")]
    public string GroupWeights { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "rpm_limit")]
    public int RpmLimit { get; set; } = 0;

    [SugarColumn(ColumnName = "enabled")]
    public bool Enabled { get; set; } = true;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "last_used_at", IsNullable = true)]
    public DateTime? LastUsedAt { get; set; }

    [SugarColumn(ColumnName = "usage_count")]
    public long UsageCount { get; set; } = 0;
}

/// <summary>
/// API 密钥验证状态表
/// </summary>
[SugarTable("orch_key_validation")]
public class KeyValidation
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "group_id", Length = 100)]
    public string GroupId { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "api_key_hash", Length = 64)]
    [Required]
    public string ApiKeyHash { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "provider_type", Length = 50)]
    [Required]
    public string ProviderType { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "is_valid")]
    public bool IsValid { get; set; } = true;

    [SugarColumn(ColumnName = "error_count")]
    public int ErrorCount { get; set; } = 0;

    [SugarColumn(ColumnName = "last_error", ColumnDataType = "TEXT")]
    public string LastError { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "last_status_code")]
    public int? LastStatusCode { get; set; }

    [SugarColumn(ColumnName = "last_validated_at")]
    public DateTime LastValidatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 密钥使用统计表
/// </summary>
[SugarTable("orch_key_usage_stats")]
public class KeyUsageStats
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "group_id", Length = 100)]
    [Required]
    public string GroupId { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "api_key_hash", Length = 64)]
    [Required]
    public string ApiKeyHash { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "usage_count")]
    [DefaultValue(0)]
    public long UsageCount { get; set; } = 0;

    [SugarColumn(ColumnName = "last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 请求日志表
/// </summary>
[SugarTable("orch_request_logs")]
public class RequestLog
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "request_id", Length = 50)]
    [Required]
    public string RequestId { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "proxy_key_id")]
    public int? ProxyKeyId { get; set; }

    [SugarColumn(ColumnName = "group_id", Length = 100)]
    public string? GroupId { get; set; }

    [SugarColumn(ColumnName = "provider_type", Length = 50)]
    public string? ProviderType { get; set; }

    [SugarColumn(ColumnName = "model", Length = 100)]
    public string? Model { get; set; }

    [SugarColumn(ColumnName = "method", Length = 10)]
    [Required]
    public string Method { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "endpoint", Length = 200)]
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "request_body", ColumnDataType = "TEXT")]
    public string? RequestBody { get; set; }

    [SugarColumn(ColumnName = "response_body", ColumnDataType = "TEXT")]
    public string? ResponseBody { get; set; }

    [SugarColumn(ColumnName = "request_headers", ColumnDataType = "TEXT")]
    public string? RequestHeaders { get; set; }

    [SugarColumn(ColumnName = "response_headers", ColumnDataType = "TEXT")]
    public string? ResponseHeaders { get; set; }

    [SugarColumn(ColumnName = "content_truncated")]
    public bool ContentTruncated { get; set; } = false;

    [SugarColumn(ColumnName = "status_code")]
    public int StatusCode { get; set; }

    [SugarColumn(ColumnName = "duration_ms")]
    public long DurationMs { get; set; }

    [SugarColumn(ColumnName = "prompt_tokens")]
    public int? PromptTokens { get; set; }

    [SugarColumn(ColumnName = "completion_tokens")]
    public int? CompletionTokens { get; set; }

    [SugarColumn(ColumnName = "total_tokens")]
    public int? TotalTokens { get; set; }

    [SugarColumn(ColumnName = "error_message", ColumnDataType = "TEXT")]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "client_ip", Length = 45)]
    public string? ClientIp { get; set; }

    [SugarColumn(ColumnName = "user_agent", Length = 500)]
    public string? UserAgent { get; set; }

    [SugarColumn(ColumnName = "openrouter_key", Length = 100)]
    public string? OpenrouterKey { get; set; }

    [SugarColumn(ColumnName = "has_tools")]
    public bool HasTools { get; set; } = false;

    [SugarColumn(ColumnName = "is_streaming")]
    public bool IsStreaming { get; set; } = false;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 系统用户表
/// </summary>
[SugarTable("orch_users")]
public class User
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "username", Length = 50)]
    [Required]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "password_hash", Length = 128)]
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "role", Length = 20)]
    public string Role { get; set; } = "admin";

    [SugarColumn(ColumnName = "enabled")]
    public bool Enabled { get; set; } = true;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "last_login_at", IsNullable = true)]
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// 用户会话表
/// </summary>
[SugarTable("orch_sessions")]
public class UserSession
{
    [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnName = "user_id")]
    public int UserId { get; set; }

    [SugarColumn(ColumnName = "token", Length = 500)]
    [Required]
    public string Token { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "expires_at")]
    public DateTime ExpiresAt { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "last_accessed_at")]
    public DateTime LastAccessedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnName = "ip_address", Length = 45)]
    public string? IpAddress { get; set; }

    [SugarColumn(ColumnName = "user_agent", Length = 500)]
    public string? UserAgent { get; set; }
}
