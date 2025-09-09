using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OrchestrationApi.Models;

#region Chat API Models

/// <summary>
/// OpenAI 兼容的聊天完成请求
/// </summary>
public class ChatCompletionRequest
{
    [Required]
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [JsonProperty("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonProperty("temperature")]
    public float? Temperature { get; set; }

    [JsonProperty("top_p")]
    public float? TopP { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; } = false;

    [JsonProperty("stop")]
    public object? Stop { get; set; }

    [JsonProperty("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonProperty("presence_penalty")]
    public float? PresencePenalty { get; set; }

    [JsonProperty("frequency_penalty")]
    public float? FrequencyPenalty { get; set; }

    [JsonProperty("logit_bias")]
    public Dictionary<string, float>? LogitBias { get; set; }

    [JsonProperty("user")]
    public string? User { get; set; }

    [JsonProperty("tools")]
    public List<ChatTool>? Tools { get; set; }

    [JsonProperty("stream_options")]
    public Dictionary<string, object>? StreamOptions { get; set; }
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
/// 聊天完成响应
/// </summary>
public class ChatCompletionResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("choices")]
    public List<ChatChoice> Choices { get; set; } = new();

    [JsonProperty("usage")]
    public TokenUsage? Usage { get; set; }

    [JsonProperty("system_fingerprint")]
    public string? SystemFingerprint { get; set; }
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

    [JsonProperty("proxy_enabled")]
    public bool ProxyEnabled { get; set; } = false;

    [JsonProperty("proxy_config")]
    public ProxyConfiguration? ProxyConfig { get; set; }
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
    public string? System { get; set; }

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