using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace OrchestrationApi.Models;

#region Chat API Models

/// <summary>
/// Chat流式选项配置
/// </summary>
public class ChatStreamOptions
{
    [JsonProperty("include_usage")]
    public bool? IncludeUsage { get; set; }
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    [Required]
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public object? Content { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonProperty("tool_call_id")]
    public string? ToolCallId { get; set; }
}

/// <summary>
/// Chat工具定义 (OpenAI格式)
/// </summary>
public class ChatTool
{
    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [Required]
    [JsonProperty("function")]
    public ChatFunction Function { get; set; } = new();
}

/// <summary>
/// Chat函数定义 (OpenAI格式)
/// </summary>
public class ChatFunction
{
    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("parameters")]
    public object? Parameters { get; set; }

    [JsonProperty("strict")]
    public bool Strict { get; set; }
}

/// <summary>
/// 工具调用
/// </summary>
public class ToolCall
{
    [Required]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [Required]
    [JsonProperty("function")]
    public FunctionCall Function { get; set; } = new();
}

/// <summary>
/// 函数调用
/// </summary>
public class FunctionCall
{
    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [JsonProperty("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>
/// 聊天选择
/// </summary>
public class ChatChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public ChatMessage? Message { get; set; }

    [JsonProperty("delta")]
    public ChatMessage? Delta { get; set; }

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Token 使用统计
/// </summary>
public class TokenUsage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

#endregion Chat API Models

#region Error Models

/// <summary>
/// API 错误响应
/// </summary>
public class ApiErrorResponse
{
    [JsonProperty("error")]
    public ApiError Error { get; set; } = new();
}

/// <summary>
/// API 错误详情
/// </summary>
public class ApiError
{
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "error";

    [JsonProperty("param")]
    public string? Param { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }
}

#endregion Error Models

#region Response Models

/// <summary>
/// 通用API响应包装类
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public class ApiResponse<T>
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("data")]
    public T? Data { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

/// <summary>
/// 无数据的API响应
/// </summary>
public class ApiResponse : ApiResponse<object>
{
}

#endregion Response Models

#region Models API Models

/// <summary>
/// 模型信息
/// </summary>
public class ModelInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = "model";

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("owned_by")]
    public string OwnedBy { get; set; } = string.Empty;

    [JsonProperty("permission")]
    public List<object>? Permission { get; set; }

    [JsonProperty("root")]
    public string? Root { get; set; }

    [JsonProperty("parent")]
    public string? Parent { get; set; }
}

/// <summary>
/// 模型列表响应
/// </summary>
public class ModelsResponse
{
    [JsonProperty("object")]
    public string Object { get; set; } = "list";

    [JsonProperty("data")]
    public List<ModelInfo> Data { get; set; } = new();
}

#endregion Models API Models

#region Admin API Models

/// <summary>
/// 登录请求
/// </summary>
public class LoginRequest
{
    [Required]
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录响应
/// </summary>
public class LoginResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("token")]
    public string? Token { get; set; }

    [JsonProperty("expires_at")]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// 系统状态响应
/// </summary>
public class SystemStatusResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = "running";

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonProperty("uptime")]
    public TimeSpan Uptime { get; set; }

    [JsonProperty("groups")]
    public List<GroupStatus> Groups { get; set; } = new();

    [JsonProperty("total_requests")]
    public long TotalRequests { get; set; }

    [JsonProperty("successful_requests")]
    public long SuccessfulRequests { get; set; }

    [JsonProperty("failed_requests")]
    public long FailedRequests { get; set; }
}

/// <summary>
/// 分组状态
/// </summary>
public class GroupStatus
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("group_name")]
    public string GroupName { get; set; } = string.Empty;

    [JsonProperty("provider_type")]
    public string ProviderType { get; set; } = string.Empty;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("total_keys")]
    public int TotalKeys { get; set; }

    [JsonProperty("valid_keys")]
    public int ValidKeys { get; set; }

    [JsonProperty("requests_count")]
    public long RequestsCount { get; set; }

    [JsonProperty("last_request_at")]
    public DateTime? LastRequestAt { get; set; }
}

/// <summary>
/// 分组创建/更新请求
/// </summary>
public class GroupRequest
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [Required]
    [JsonProperty("group_name")]
    public string GroupName { get; set; } = string.Empty;

    [Required]
    [JsonProperty("provider_type")]
    public string ProviderType { get; set; } = string.Empty;

    [JsonProperty("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    [JsonProperty("api_keys")]
    public List<string> ApiKeys { get; set; } = new();

    [JsonProperty("models")]
    public List<string> Models { get; set; } = new();

    [JsonProperty("model_aliases")]
    public Dictionary<string, string> ModelAliases { get; set; } = new();

    [JsonProperty("parameter_overrides")]
    public Dictionary<string, object> ParameterOverrides { get; set; } = new();

    [JsonProperty("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [JsonProperty("balance_policy")]
    public string BalancePolicy { get; set; } = "round_robin";

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; } = 3;

    [JsonProperty("timeout")]
    public int Timeout { get; set; } = 60;

    [JsonProperty("rpm_limit")]
    public int RpmLimit { get; set; } = 0;

    [JsonProperty("test_model")]
    public string? TestModel { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; } = 0;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("fake_streaming")]
    public bool FakeStreaming { get; set; } = false;

    [JsonProperty("proxy_enabled")]
    public bool ProxyEnabled { get; set; } = false;

    [JsonProperty("proxy_config")]
    public ProxyConfiguration? ProxyConfig { get; set; }

    [JsonProperty("health_check_enabled")]
    public bool HealthCheckEnabled { get; set; } = true;
}

/// <summary>
/// 代理配置模型
/// </summary>
public class ProxyConfiguration
{
    [JsonProperty("type")]
    public string Type { get; set; } = "http";

    [JsonProperty("host")]
    public string Host { get; set; } = string.Empty;

    [JsonProperty("port")]
    public int Port { get; set; } = 8080;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;

    [JsonProperty("bypass_local")]
    public bool BypassLocal { get; set; } = true;

    [JsonProperty("bypass_domains")]
    public List<string> BypassDomains { get; set; } = new();
}

/// <summary>
/// API密钥请求
/// </summary>
public class ApiKeyRequest
{
    [Required]
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

/// <summary>
/// 批量添加密钥请求
/// </summary>
public class BatchAddKeysRequest
{
    [Required]
    [JsonProperty("keys")]
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// 批量添加密钥结果
/// </summary>
public class BatchAddKeysResult
{
    [JsonProperty("success_count")]
    public int SuccessCount { get; set; }

    [JsonProperty("skipped_count")]
    public int SkippedCount { get; set; }

    [JsonProperty("errors")]
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 代理密钥请求
/// </summary>
public class ProxyKeyRequest
{
    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }
}

/// <summary>
/// 更新代理密钥请求
/// </summary>
public class UpdateProxyKeyRequest
{
    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("allowed_groups")]
    public List<string> AllowedGroups { get; set; } = new();

    [JsonProperty("group_balance_policy")]
    public string? GroupBalancePolicy { get; set; }

    [JsonProperty("rpm_limit")]
    public int? RpmLimit { get; set; }

    [JsonProperty("group_selection_config")]
    public GroupSelectionConfig? GroupSelectionConfig { get; set; }
}

/// <summary>
/// 分组选择配置
/// </summary>
public class GroupSelectionConfig
{
    [JsonProperty("strategy")]
    public string Strategy { get; set; } = "round_robin";

    [JsonProperty("group_weights")]
    public List<GroupWeight>? GroupWeights { get; set; }
}

/// <summary>
/// 分组权重配置
/// </summary>
public class GroupWeight
{
    [JsonProperty("group_id")]
    public string GroupId { get; set; } = string.Empty;

    [JsonProperty("weight")]
    public int Weight { get; set; } = 1;
}

/// <summary>
/// API密钥信息
/// </summary>
public class ApiKeyInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("usage_count")]
    public int UsageCount { get; set; }

    [JsonProperty("error_count")]
    public int ErrorCount { get; set; }

    [JsonProperty("last_used")]
    public DateTime? LastUsed { get; set; }

    [JsonProperty("last_error")]
    public string? LastError { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 代理密钥信息
/// </summary>
public class ProxyKeyInfo
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("usage_count")]
    public int UsageCount { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("allowed_groups")]
    public List<string> AllowedGroups { get; set; } = new();

    [JsonProperty("group_balance_policy")]
    public string? GroupBalancePolicy { get; set; }

    [JsonProperty("rpm_limit")]
    public int RpmLimit { get; set; } = 0;

    [JsonProperty("group_selection_config")]
    public GroupSelectionConfig? GroupSelectionConfig { get; set; }
}

/// <summary>
/// 密钥统计信息
/// </summary>
public class KeyStatistics
{
    [JsonProperty("total_keys")]
    public int TotalKeys { get; set; }

    [JsonProperty("active_keys")]
    public int ActiveKeys { get; set; }

    [JsonProperty("total_requests")]
    public long TotalRequests { get; set; }

    [JsonProperty("total_errors")]
    public long TotalErrors { get; set; }
}

/// <summary>
/// 认证验证响应
/// </summary>
public class AuthVerifyResponse
{
    [JsonProperty("valid")]
    public bool Valid { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("user")]
    public UserInfo? User { get; set; }
}

/// <summary>
/// 用户信息
/// </summary>
public class UserInfo
{
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// 验证密钥请求
/// </summary>
public class ValidateKeysRequest
{
    [Required]
    [JsonProperty("api_keys")]
    public List<string> ApiKeys { get; set; } = new();
}

/// <summary>
/// 导出分组请求
/// </summary>
public class ExportGroupsRequest
{
    [JsonProperty("group_ids")]
    public List<string> GroupIds { get; set; } = new();
}

/// <summary>
/// 导入分组结果
/// </summary>
public class ImportGroupsResult
{
    [JsonProperty("imported_count")]
    public int ImportedCount { get; set; }

    [JsonProperty("total_groups")]
    public int TotalGroups { get; set; }

    [JsonProperty("errors")]
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 按类型获取模型请求
/// </summary>
public class GetModelsByTypeRequest
{
    [Required]
    [JsonProperty("provider_type")]
    public string ProviderType { get; set; } = string.Empty;

    [JsonProperty("base_url")]
    public string? BaseUrl { get; set; }

    [Required]
    [JsonProperty("api_keys")]
    public List<string> ApiKeys { get; set; } = new();

    [JsonProperty("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [JsonProperty("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [JsonProperty("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// 强制更新密钥状态请求
/// </summary>
public class ForceUpdateKeyStatusRequest
{
    [Required]
    [JsonProperty("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 批量删除日志请求
/// </summary>
public class BatchDeleteLogsRequest
{
    [Required]
    [JsonProperty("ids")]
    public List<int> Ids { get; set; } = new();
}

/// <summary>
/// 日志响应DTO - 用于前端显示
/// </summary>
public class LogResponseDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonProperty("proxy_key_name")]
    public string? ProxyKeyName { get; set; }

    [JsonProperty("proxy_key_id")]
    public int? ProxyKeyId { get; set; }

    [JsonProperty("provider_group")]
    public string? ProviderGroup { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("status_code")]
    public int StatusCode { get; set; }

    [JsonProperty("duration")]
    public long Duration { get; set; } // DurationMs

    [JsonProperty("tokens_used")]
    public int? TokensUsed { get; set; } // TotalTokens

    [JsonProperty("client_ip")]
    public string? ClientIp { get; set; }

    [JsonProperty("openrouter_key")]
    public string? OpenrouterKey { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; } // ErrorMessage

    [JsonProperty("is_stream")]
    public bool IsStream { get; set; } // IsStreaming

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("request_body")]
    public string? RequestBody { get; set; }

    [JsonProperty("response_body")]
    public string? ResponseBody { get; set; }

    [JsonProperty("request_headers")]
    public string? RequestHeaders { get; set; }

    [JsonProperty("response_headers")]
    public string? ResponseHeaders { get; set; }

    [JsonProperty("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int? TotalTokens { get; set; }

    [JsonProperty("has_tools")]
    public bool HasTools { get; set; }

    [JsonProperty("content_truncated")]
    public bool ContentTruncated { get; set; }
}

#endregion Admin API Models

#region Validation Models

/// <summary>
/// 模型验证结果
/// </summary>
public class ModelValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int AvailableProvidersCount { get; set; }
}

/// <summary>
/// 修改密码请求
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    [JsonProperty("current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [JsonProperty("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 更新用户信息请求
/// </summary>
public class UpdateUserRequest
{
    [Required]
    [JsonProperty("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonProperty("newUsername")]
    public string? NewUsername { get; set; }

    [MinLength(6)]
    [JsonProperty("newPassword")]
    public string? NewPassword { get; set; }
}

#endregion Validation Models

#region Gemini API Models

/// <summary>
/// Gemini 生成内容请求
/// </summary>
public class GeminiGenerateContentRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [JsonProperty("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonProperty("systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonProperty("safetySettings")]
    public List<GeminiSafetySetting>? SafetySettings { get; set; }

    [JsonProperty("tools")]
    public List<GeminiTool>? Tools { get; set; }

    [JsonProperty("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

/// <summary>
/// Gemini 内容
/// </summary>
public class GeminiContent
{
    [JsonProperty("parts")]
    public List<GeminiPart> Parts { get; set; } = new();

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Gemini 内容部分
/// </summary>
public class GeminiPart
{
    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("inlineData")]
    public GeminiInlineData? InlineData { get; set; }

    [JsonProperty("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }

    [JsonProperty("functionResponse")]
    public GeminiFunctionResponse? FunctionResponse { get; set; }
}

/// <summary>
/// Gemini 内联数据（用于图片等多模态数据）
/// </summary>
public class GeminiInlineData
{
    [JsonProperty("data")]
    public string Data { get; set; } = string.Empty;

    [JsonProperty("mimeType")]
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// Gemini 安全设置
/// </summary>
public class GeminiSafetySetting
{
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("threshold")]
    public string Threshold { get; set; } = string.Empty;
}

/// <summary>
/// Gemini 工具
/// </summary>
public class GeminiTool
{
    [JsonProperty("functionDeclarations")]
    public List<GeminiFunctionDeclaration>? FunctionDeclarations { get; set; }
}

/// <summary>
/// Gemini 函数声明
/// </summary>
public class GeminiFunctionDeclaration
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("parameters")]
    public GeminiSchema? Parameters { get; set; }
}

/// <summary>
/// Gemini 模式定义
/// </summary>
public class GeminiSchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("properties")]
    public Dictionary<string, GeminiPropertySchema>? Properties { get; set; }

    [JsonProperty("required")]
    public List<string>? Required { get; set; }
}

/// <summary>
/// Gemini 属性模式
/// </summary>
public class GeminiPropertySchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("enum")]
    public List<string>? Enum { get; set; }
}

/// <summary>
/// Gemini 函数调用
/// </summary>
public class GeminiFunctionCall
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("args")]
    public Dictionary<string, object>? Args { get; set; }
}

/// <summary>
/// Gemini 函数响应
/// </summary>
public class GeminiFunctionResponse
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("response")]
    public Dictionary<string, object>? Response { get; set; }
}

/// <summary>
/// Gemini 生成配置
/// </summary>
public class GeminiGenerationConfig
{
    [JsonProperty("temperature")]
    public float? Temperature { get; set; }

    [JsonProperty("topP")]
    public float? TopP { get; set; }

    [JsonProperty("topK")]
    public int? TopK { get; set; }

    [JsonProperty("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonProperty("candidateCount")]
    public int? CandidateCount { get; set; }

    [JsonProperty("stopSequences")]
    public List<string>? StopSequences { get; set; }

    [JsonProperty("thinkingConfig")]
    public GeminiThinkingConfig? ThinkingConfig { get; set; }
}

/// <summary>
/// Gemini 思考配置
/// </summary>
public class GeminiThinkingConfig
{
    [JsonProperty("includeThoughts")]
    public bool IncludeThoughts { get; set; }

    [JsonProperty("thinkingBudget")]
    public int? ThinkingBudget { get; set; }
}

/// <summary>
/// Gemini 原生模型信息
/// </summary>
public class GeminiNativeModelInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("inputTokenLimit")]
    public int? InputTokenLimit { get; set; }

    [JsonProperty("outputTokenLimit")]
    public int? OutputTokenLimit { get; set; }

    [JsonProperty("supportedGenerationMethods")]
    public List<string>? SupportedGenerationMethods { get; set; }

    [JsonProperty("temperature")]
    public float? Temperature { get; set; }

    [JsonProperty("topP")]
    public float? TopP { get; set; }

    [JsonProperty("topK")]
    public int? TopK { get; set; }

    [JsonProperty("maxTemperature")]
    public float? MaxTemperature { get; set; }

    [JsonProperty("thinking")]
    public bool? Thinking { get; set; }
}

/// <summary>
/// Gemini 原生模型列表响应
/// </summary>
public class GeminiNativeModelsResponse
{
    [JsonProperty("models")]
    public List<GeminiNativeModelInfo> Models { get; set; } = new();

    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
}

#endregion Gemini API Models

#region Anthropic Claude API Models

/// <summary>
/// Claude 原生 API 消息内容
/// </summary>
public class AnthropicContent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("text")]
    public string? Text { get; set; }

    // 图像内容支持
    [JsonProperty("source")]
    public AnthropicImageSource? Source { get; set; }

    // 工具调用内容
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("input")]
    public Dictionary<string, object>? Input { get; set; }

    // 工具结果内容
    [JsonProperty("tool_use_id")]
    public string? ToolUseId { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("is_error")]
    public bool? IsError { get; set; }
}

/// <summary>
/// Claude 图像源定义
/// </summary>
public class AnthropicImageSource
{
    [JsonProperty("type")]
    public string Type { get; set; } = "base64";

    [JsonProperty("media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty("data")]
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Claude 原生 API 消息
/// </summary>
public class AnthropicMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    [JsonConverter(typeof(AnthropicContentListConverter))]
    public List<AnthropicContent> Content { get; set; } = new();
}

/// <summary>
/// Claude 原生 API 请求
/// </summary>
public class AnthropicMessageRequest
{
    [Required]
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; }

    [Required]
    [JsonProperty("messages")]
    public List<AnthropicMessage> Messages { get; set; } = new();

    [JsonProperty("system")]
    public JToken? System { get; set; }

    [JsonProperty("temperature")]
    public float? Temperature { get; set; }

    [JsonProperty("top_p")]
    public float? TopP { get; set; }

    [JsonProperty("top_k")]
    public int? TopK { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;

    [JsonProperty("stop_sequences")]
    public List<string>? StopSequences { get; set; }

    [JsonProperty("tools")]
    public List<AnthropicTool>? Tools { get; set; }

    [JsonProperty("tool_choice")]
    public AnthropicToolChoice? ToolChoice { get; set; }
}

/// <summary>
/// Claude 工具定义
/// </summary>
public class AnthropicTool
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("input_schema")]
    public AnthropicInputSchema InputSchema { get; set; } = new();
}

/// <summary>
/// Claude 工具输入架构
/// </summary>
public class AnthropicInputSchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = "object";

    [JsonProperty("properties")]
    public Dictionary<string, AnthropicProperty>? Properties { get; set; }

    [JsonProperty("required")]
    public List<string>? Required { get; set; }
}

/// <summary>
/// Claude 工具属性定义
/// </summary>
public class AnthropicProperty
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("enum")]
    public List<string>? Enum { get; set; }
}

/// <summary>
/// Claude 工具选择
/// </summary>
public class AnthropicToolChoice
{
    [JsonProperty("type")]
    public string Type { get; set; } = "auto";

    [JsonProperty("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Claude 原生 API 使用统计
/// </summary>
public class AnthropicUsage
{
    [JsonProperty("input_tokens")]
    public int InputTokens { get; set; }

    [JsonProperty("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>
/// Claude 原生 API 响应内容
/// </summary>
public class AnthropicResponseContent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("text")]
    public string? Text { get; set; }

    // 工具调用响应内容
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("input")]
    public Dictionary<string, object>? Input { get; set; }
}

/// <summary>
/// Claude 原生 API 响应
/// </summary>
public class AnthropicMessageResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "message";

    [JsonProperty("role")]
    public string Role { get; set; } = "assistant";

    [JsonProperty("content")]
    public List<AnthropicResponseContent> Content { get; set; } = new();

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("stop_reason")]
    public string? StopReason { get; set; }

    [JsonProperty("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonProperty("usage")]
    public AnthropicUsage? Usage { get; set; }
}

/// <summary>
/// Claude 原生 API 流式事件
/// </summary>
public class AnthropicStreamEvent
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("message")]
    public AnthropicMessageResponse? Message { get; set; }

    [JsonProperty("content_block")]
    public AnthropicResponseContent? ContentBlock { get; set; }

    [JsonProperty("delta")]
    public Dictionary<string, object>? Delta { get; set; }

    [JsonProperty("usage")]
    public AnthropicUsage? Usage { get; set; }
}

#endregion Anthropic Claude API Models

#region Responses API Models

/// <summary>
/// Responses API 请求（支持多种输入格式和工具类型）
/// </summary>
public class ResponsesRequest
{
    [Required]
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [JsonProperty("input")]
    [JsonConverter(typeof(ResponsesInputConverter))]
    public object Input { get; set; } = string.Empty;

    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    [JsonProperty("tools")]
    public List<ResponsesTool>? Tools { get; set; }

    [JsonProperty("tool_choice")]
    public string? ToolChoice { get; set; }

    [JsonProperty("reasoning")]
    public ResponsesReasoningConfig? Reasoning { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;

    [JsonProperty("temperature")]
    public float? Temperature { get; set; }

    [JsonProperty("top_p")]
    public float? TopP { get; set; }

    [JsonProperty("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonProperty("stop")]
    public object? Stop { get; set; }

    // 新增：支持响应链式调用
    [JsonProperty("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    // 新增：后台任务支持
    [JsonProperty("background")]
    public bool Background { get; set; } = false;

    // 新增：存储设置
    [JsonProperty("store")]
    public bool Store { get; set; } = true;

    // 新增：并行工具调用
    [JsonProperty("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    // 新增：包含字段配置
    [JsonProperty("include")]
    public List<string>? Include { get; set; }

    // 新增：元数据
    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    // 新增：推理努力配置
    [JsonProperty("reasoning_effort")]
    public string? ReasoningEffort { get; set; }

    // 新增：截断配置
    [JsonProperty("truncation")]
    public ResponsesTruncationConfig? Truncation { get; set; }
}

/// <summary>
/// Responses API 工具定义
/// </summary>
public class ResponsesTool
{
    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("parameters")]
    public object? Parameters { get; set; }

    // File Search Tool 配置
    [JsonProperty("vector_store_ids")]
    public List<string>? VectorStoreIds { get; set; }

    [JsonProperty("max_num_results")]
    public int? MaxNumResults { get; set; }

    // Code Interpreter Tool 配置
    [JsonProperty("container")]
    public ResponsesCodeContainer? Container { get; set; }

    // Image Generation Tool 配置
    [JsonProperty("partial_images")]
    public int? PartialImages { get; set; }

    // MCP Tool 配置
    [JsonProperty("server_label")]
    public string? ServerLabel { get; set; }

    [JsonProperty("server_url")]
    public string? ServerUrl { get; set; }

    [JsonProperty("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonProperty("require_approval")]
    public string? RequireApproval { get; set; }
}

/// <summary>
/// Code Interpreter 容器配置
/// </summary>
public class ResponsesCodeContainer
{
    [JsonProperty("type")]
    public string Type { get; set; } = "auto";

    [JsonProperty("files")]
    public List<string>? Files { get; set; }
}

/// <summary>
/// Responses API 推理配置
/// </summary>
public class ResponsesReasoningConfig
{
    [JsonProperty("effort")]
    public string? Effort { get; set; }
}

/// <summary>
/// Responses API 截断配置
/// </summary>
public class ResponsesTruncationConfig
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("last_messages")]
    public int? LastMessages { get; set; }
}

/// <summary>
/// Responses API 输入内容项
/// </summary>
public class ResponsesInputContent
{
    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("image_url")]
    public string? ImageUrl { get; set; }

    [JsonProperty("file_url")]
    public string? FileUrl { get; set; }
}

/// <summary>
/// Responses API 输入消息
/// </summary>
public class ResponsesInputMessage
{
    [Required]
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [Required]
    [JsonProperty("content")]
    public List<ResponsesInputContent> Content { get; set; } = new();
}

/// <summary>
/// Responses API 响应对象
/// </summary>
public class ResponsesApiResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = "response";

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = "completed";

    [JsonProperty("output")]
    public List<object>? Output { get; set; }

    [JsonProperty("output_text")]
    public string? OutputText { get; set; }

    [JsonProperty("usage")]
    public ResponsesUsage? Usage { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonProperty("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    [JsonProperty("reasoning")]
    public object? Reasoning { get; set; }

    [JsonProperty("error")]
    public object? Error { get; set; }

    [JsonProperty("incomplete_details")]
    public object? IncompleteDetails { get; set; }
}

/// <summary>
/// Responses API 使用统计
/// </summary>
public class ResponsesUsage
{
    [JsonProperty("input_tokens")]
    public int InputTokens { get; set; }

    [JsonProperty("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonProperty("output_tokens_details")]
    public ResponsesTokenDetails? OutputTokensDetails { get; set; }
}

/// <summary>
/// Token详情
/// </summary>
public class ResponsesTokenDetails
{
    [JsonProperty("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

#endregion Responses API Models

/// <summary>
/// Newtonsoft.Json 转换器：支持 Responses API 输入格式
/// - 字符串 => 基础文本请求
/// - 数组   => 消息格式（包含图像、文件等内容）
/// </summary>
public sealed class ResponsesInputConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(object);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        // null/undefined -> empty string
        if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
        {
            return string.Empty;
        }

        // string -> basic text request
        if (token.Type == JTokenType.String)
        {
            return token.ToString();
        }

        // array -> message format with content
        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<ResponsesInputMessage>>(serializer) ?? new List<ResponsesInputMessage>();
        }

        throw new JsonSerializationException($"Unsupported input token type: {token.Type}");
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}

/// <summary>
/// Newtonsoft.Json 转换器：支持 Anthropic 消息 content 接受字符串或数组
/// - 字符串 => [ { type: "text", text: "..." } ]
/// - 对象   => [ { ... } ]
/// - 数组   => 原样反序列化为 List<AnthropicContent>
/// </summary>
public sealed class AnthropicContentListConverter : JsonConverter<List<AnthropicContent>>
{
    public override List<AnthropicContent>? ReadJson(JsonReader reader, Type objectType, List<AnthropicContent>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        // null/undefined -> empty list
        if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
        {
            return new List<AnthropicContent>();
        }

        // string -> single text block
        if (token.Type == JTokenType.String)
        {
            return new List<AnthropicContent>
            {
                new AnthropicContent { Type = "text", Text = token.ToString() }
            };
        }

        // allow basic primitives to be coerced to text
        if (token.Type is JTokenType.Integer or JTokenType.Float or JTokenType.Boolean)
        {
            return new List<AnthropicContent>
            {
                new AnthropicContent { Type = "text", Text = token.ToString() }
            };
        }

        // object -> single content block
        if (token.Type == JTokenType.Object)
        {
            var single = token.ToObject<AnthropicContent>(serializer) ?? new AnthropicContent();
            return new List<AnthropicContent> { single };
        }

        // array -> list of content blocks
        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<AnthropicContent>>(serializer) ?? new List<AnthropicContent>();
        }

        throw new JsonSerializationException($"Unsupported content token type: {token.Type}");
    }

    public override void WriteJson(JsonWriter writer, List<AnthropicContent>? value, JsonSerializer serializer)
    {
        // Always serialize as an array of content blocks
        serializer.Serialize(writer, value ?? new List<AnthropicContent>());
    }
}

#region Serilog System Log Models

/// <summary>
/// Serilog日志数据库模型
/// </summary>
public class SerilogLog
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 日志时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 日志级别
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// 日志消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// 日志属性（JSON格式）
    /// </summary>
    public string? Properties { get; set; }

    /// <summary>
    /// 日志模板
    /// </summary>
    public string? LogEvent { get; set; }
}

/// <summary>
/// 日志查询请求参数
/// </summary>
public class LogQueryRequest
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页记录数
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// 日志级别筛选
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// 关键字搜索（搜索消息和异常）
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 日志查询响应
/// </summary>
public class LogQueryResponse
{
    /// <summary>
    /// 总记录数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页记录数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// 日志列表
    /// </summary>
    public List<SerilogLog> Logs { get; set; } = new();
}

/// <summary>
/// 日志统计信息
/// </summary>
public class LogStatistics
{
    /// <summary>
    /// 总日志数
    /// </summary>
    public int TotalLogs { get; set; }

    /// <summary>
    /// 按级别统计
    /// </summary>
    public Dictionary<string, int> LogsByLevel { get; set; } = new();

    /// <summary>
    /// 最近24小时日志数
    /// </summary>
    public int Last24Hours { get; set; }

    /// <summary>
    /// 最近1小时日志数
    /// </summary>
    public int LastHour { get; set; }

    /// <summary>
    /// 数据库大小（字节）
    /// </summary>
    public long DatabaseSize { get; set; }

    /// <summary>
    /// 最早日志时间
    /// </summary>
    public DateTime? EarliestLog { get; set; }

    /// <summary>
    /// 最新日志时间
    /// </summary>
    public DateTime? LatestLog { get; set; }
}

#endregion