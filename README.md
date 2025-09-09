# OrchestrationApi

一个基于 .NET 9 的企业级多服务商 AI API 代理服务，提供统一的 OpenAI 兼容接口，支持智能路由、故障转移、负载均衡等企业级功能。

## 🚀 核心特性

### 多服务商支持
- **OpenAI**: GPT-3.5、GPT-4、GPT-4 Turbo 等全系列模型
- **Anthropic Claude**: Claude 3 Haiku、Claude 3 Sonnet、Claude 3 Opus
- **Google Gemini**: Gemini Pro、Gemini Vision 等
- **可扩展架构**: 轻松添加更多 AI 服务商

### 智能路由与负载均衡
- **智能路由**: 基于模型、负载、健康状态自动选择最佳提供商
- **多种负载均衡策略**: 轮询(round_robin)、随机(random)、最少使用(least_used)
- **故障转移**: 自动重试机制，无感切换到备用提供商
- **健康检查**: 实时监控 API 密钥状态和服务可用性

### OpenAI 完全兼容
- **无缝替换**: 完全兼容 OpenAI API 格式，直接替换 base_url 即可使用
- **流式响应**: 完整的 Server-Sent Events 支持
- **函数调用**: 完整的 Function Calling 和 Tools 支持
- **参数透传**: 支持所有 OpenAI 参数，包括 temperature、top_p 等

### 企业级管理功能
- **Web 管理界面**: 现代化的管理仪表板，实时监控和配置
- **多级代理密钥**: 灵活的密钥管理和权限控制
- **请求日志**: 详细的请求统计和分析
- **实时监控**: 系统状态、性能指标、错误统计

### 高性能与可扩展性
- **异步架构**: 全异步设计，支持高并发请求
- **连接池**: HTTP 客户端连接池优化
- **缓存支持**: 内存缓存
- **Docker 支持**: 完整的容器化部署方案

## 📋 系统要求

- .NET 9 SDK 或 Docker
- SQLite（默认）或 MySQL 数据库

## 🚀 快速开始

### 使用 Docker Compose（推荐）

1. **准备部署文件**
```bash
# 下载 docker-compose.yml
wget https://raw.githubusercontent.com/your-repo/OrchestrationApi/main/docker-compose.yml

# 创建配置文件
mkdir -p config
cp appsettings.json config/appsettings.Production.json
```

2. **启动服务**
```bash
# 启动完整环境（包含 MySQL）
docker-compose up -d

# 或仅启动 API 服务（使用 SQLite）
docker-compose up -d orchestration-api
```

3. **访问服务**
- 管理界面: http://localhost:5000/dashboard
- API 文档: http://localhost:5000/swagger
- 健康检查: http://localhost:5000/health

### 本地开发

1. **克隆项目**
```bash
git clone <repository-url>
cd OrchestrationApi
```

2. **安装依赖**
```bash
dotnet restore
```

3. **配置数据库**
```bash
# SQLite（默认，无需额外配置）
# 或编辑 appsettings.json 配置 MySQL
```

4. **运行服务**
```bash
# 开发模式
dotnet run

# 或使用热重载
dotnet watch run
```

## 🔧 配置指南

### 基础配置 (appsettings.json)

```json
{
  "OrchestrationApi": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5000
    },
    "Auth": {
      "Username": "admin",
      "Password": "your-secure-password",
      "JwtSecret": "your-jwt-secret-key-change-in-production"
    },
    "Database": {
      "Type": "sqlite",
      "ConnectionString": "Data Source=Data/orchestration_api.db"
    },
    "Global": {
      "Timeout": 60,
      "Retries": 3,
      "BalancePolicy": "round_robin"
    }
  }
}
```

### MySQL 数据库配置

```json
{
  "OrchestrationApi": {
    "Database": {
      "Type": "mysql",
      "MySqlConnectionString": "Server=localhost;Database=orchestration_api;Uid=root;Pwd=password;"
    }
  }
}
```


## 📖 API 使用指南

### 聊天完成接口

与 OpenAI API 完全兼容，支持所有参数：

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer your-proxy-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-3.5-turbo",
    "messages": [
      {"role": "user", "content": "Hello, world!"}
    ],
    "temperature": 0.7,
    "max_tokens": 1000,
    "stream": false
  }'
```

### 流式响应

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer your-proxy-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {"role": "user", "content": "Tell me a story"}
    ],
    "stream": true
  }'
```

### 函数调用（Function Calling）

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer your-proxy-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {"role": "user", "content": "What is the weather like in Beijing?"}
    ],
    "tools": [
      {
        "type": "function",
        "function": {
          "name": "get_weather",
          "description": "Get weather information for a city",
          "parameters": {
            "type": "object",
            "properties": {
              "city": {"type": "string", "description": "City name"}
            },
            "required": ["city"]
          }
        }
      }
    ],
    "tool_choice": "auto"
  }'
```

### 模型列表

```bash
curl -X GET http://localhost:5000/v1/models \
  -H "Authorization: Bearer your-proxy-key"
```

## 🔐 管理 API

### 系统状态查询

```bash
curl -X GET http://localhost:5000/admin/status
```

### 提供商分组管理

```bash
# 获取分组列表
curl -X GET http://localhost:5000/admin/groups

# 创建 OpenAI 分组
curl -X POST http://localhost:5000/admin/groups \
  -H "Content-Type: application/json" \
  -d '{
    "groupName": "openai-primary",
    "providerType": "openai",
    "apiKeys": ["sk-your-openai-key-1", "sk-your-openai-key-2"],
    "models": ["gpt-3.5-turbo", "gpt-4", "gpt-4-turbo"],
    "balancePolicy": "round_robin",
    "retryCount": 3,
    "timeout": 60,
    "priority": 1,
    "enabled": true,
    "parameters": {
      "temperature": 0.7,
      "max_tokens": 2000
    }
  }'

# 更新分组
curl -X PUT http://localhost:5000/admin/groups/1 \
  -H "Content-Type: application/json" \
  -d '{...}'

# 删除分组
curl -X DELETE http://localhost:5000/admin/groups/1
```

### 代理密钥管理

```bash
# 创建代理密钥
curl -X POST http://localhost:5000/admin/proxy-keys \
  -H "Content-Type: application/json" \
  -d '{
    "name": "my-app-key",
    "rpm": 100,
    "enabled": true
  }'
```

## 💻 客户端集成示例

### Python (OpenAI SDK)

```python
from openai import OpenAI

# 初始化客户端
client = OpenAI(
    api_key="your-proxy-key",
    base_url="http://localhost:5000/v1"
)

# 聊天完成
response = client.chat.completions.create(
    model="gpt-3.5-turbo",
    messages=[
        {"role": "user", "content": "Hello, world!"}
    ],
    temperature=0.7,
    max_tokens=1000
)

print(response.choices[0].message.content)

# 流式响应
stream = client.chat.completions.create(
    model="gpt-4",
    messages=[
        {"role": "user", "content": "Tell me a story"}
    ],
    stream=True
)

for chunk in stream:
    if chunk.choices[0].delta.content is not None:
        print(chunk.choices[0].delta.content, end="")
```

### Node.js

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'your-proxy-key',
  baseURL: 'http://localhost:5000/v1'
});

// 基础聊天
const completion = await openai.chat.completions.create({
  model: 'gpt-3.5-turbo',
  messages: [
    { role: 'user', content: 'Hello, world!' }
  ],
  temperature: 0.7
});

console.log(completion.choices[0].message.content);

// 流式响应
const stream = await openai.chat.completions.create({
  model: 'gpt-4',
  messages: [
    { role: 'user', content: 'Tell me a story' }
  ],
  stream: true
});

for await (const chunk of stream) {
  process.stdout.write(chunk.choices[0]?.delta?.content || '');
}
```

### Go

```go
package main

import (
    "context"
    "fmt"
    "github.com/sashabaranov/go-openai"
)

func main() {
    config := openai.DefaultConfig("your-proxy-key")
    config.BaseURL = "http://localhost:5000/v1"
    client := openai.NewClientWithConfig(config)

    resp, err := client.CreateChatCompletion(
        context.Background(),
        openai.ChatCompletionRequest{
            Model: openai.GPT3Dot5Turbo,
            Messages: []openai.ChatCompletionMessage{
                {
                    Role:    openai.ChatMessageRoleUser,
                    Content: "Hello, world!",
                },
            },
        },
    )

    if err != nil {
        fmt.Printf("Error: %v\n", err)
        return
    }

    fmt.Println(resp.Choices[0].Message.Content)
}
```

## 🐳 Docker 部署

### 基础部署

```yaml
version: '3.8'
services:
  orchestration-api:
    image: orchestration-api:latest
    ports:
      - "5000:5000"
    environment:
      - OrchestrationApi__Auth__Password=your-secure-password
      - OrchestrationApi__Auth__JwtSecret=your-jwt-secret
    volumes:
      - ./data:/app/Data
      - ./logs:/app/logs
    restart: unless-stopped
```

### 完整生产环境

```yaml
version: '3.8'
services:
  orchestration-api:
    image: orchestration-api:latest
    ports:
      - "5000:5000"
    environment:
      - OrchestrationApi__Database__Type=mysql
      - OrchestrationApi__Database__MySqlConnectionString=Server=mysql;Database=orchestration_api;Uid=orchestration;Pwd=secure_password;
      - OrchestrationApi__Auth__Password=your-secure-password
    depends_on:
      - mysql
    restart: unless-stopped

  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: root_password
      MYSQL_DATABASE: orchestration_api
      MYSQL_USER: orchestration
      MYSQL_PASSWORD: secure_password
    volumes:
      - mysql_data:/var/lib/mysql
    restart: unless-stopped

volumes:
  mysql_data:
```

## 🔍 监控与运维

### 健康检查端点

- `/health` - 基础健康检查
- `/health/ready` - 就绪检查（Kubernetes）
- `/health/live` - 存活检查（Kubernetes）
- `/admin/status` - 详细系统状态

### 日志管理

系统使用 Serilog 进行结构化日志记录：

```bash
# 查看实时日志
tail -f logs/orchestration-api-$(date +%Y%m%d).log

# Docker 环境日志
docker logs -f orchestration-api
```

### 性能监控

系统提供多个监控指标：

- 请求处理时间
- 成功/失败率
- 提供商响应时间
- 系统资源使用情况

### 备份策略

```bash
# SQLite 数据库备份
cp Data/orchestration_api.db backup/db-$(date +%Y%m%d-%H%M%S).db

# MySQL 备份
docker exec mysql mysqldump -u orchestration -p orchestration_api > backup/db-$(date +%Y%m%d-%H%M%S).sql
```

## 🛡️ 安全最佳实践

1. **更改默认凭据**: 修改默认管理员用户名和密码
2. **使用强密钥**: 设置复杂的 JWT 密钥和数据库密码
3. **启用 HTTPS**: 生产环境必须使用 HTTPS
4. **网络隔离**: 限制数据库的网络访问
5. **定期备份**: 实施自动化备份策略
6. **密钥轮换**: 定期更换 API 密钥
7. **访问控制**: 使用防火墙限制访问源

## 🚨 故障排除

### 常见问题

**1. 数据库连接失败**
```bash
# 检查数据库服务状态
docker ps | grep mysql
# 验证连接字符串
```

**2. API 密钥验证失败**
```bash
# 查看密钥验证日志
grep "Key validation failed" logs/orchestration-api-*.log
```

**3. 请求超时**
```bash
# 检查网络连接
curl -I https://api.openai.com/v1/models
# 调整超时设置
```

**4. 内存使用过高**
```bash
# 监控内存使用
docker stats orchestration-api
# 检查日志文件大小
du -h logs/
```

### 诊断命令

```bash
# 系统状态检查
curl http://localhost:5000/admin/status

# 查看提供商状态
curl http://localhost:5000/admin/groups

# 检查代理密钥
curl http://localhost:5000/admin/proxy-keys

# 测试 API 可用性
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer test-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-3.5-turbo", "messages": [{"role": "user", "content": "test"}]}'
```

## 📈 性能优化

### 配置优化

```json
{
  "OrchestrationApi": {
    "Global": {
      "Timeout": 30,
      "Retries": 2,
      "BalancePolicy": "least_used"
    },
    "RequestLogging": {
      "Enabled": true,
      "EnableDetailedContent": false,
      "MaxContentLength": 1000
    }
  }
}
```

### 扩展建议

1. **负载均衡**: 使用 Nginx 或云负载均衡器
2. **缓存策略**: 启用内存缓存常用响应
3. **数据库优化**: 使用连接池和索引优化
4. **监控告警**: 集成 Prometheus + Grafana

## 🤝 贡献指南

我们欢迎社区贡献！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 开启 Pull Request

### 开发规范

- 遵循 C# 编码规范
- 添加适当的单元测试
- 更新相关文档
- 确保所有测试通过

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

## 📞 支持与反馈

- **文档**: 查看项目文档和 FAQ
- **问题反馈**: 提交 GitHub Issue
- **功能建议**: 通过 Issue 提出新功能需求
- **社区讨论**: 参与 GitHub Discussions

## 🗓️ 更新日志

### v2.0.0 (2024-12-20)
- 升级到 .NET 9
- 新增 Web 管理界面
- 完善的 Docker 支持
- 增强的监控和日志功能

### v1.0.0 (2024-12-01)
- 初始版本发布
- 支持 OpenAI、Anthropic、Google Gemini
- 基础管理功能
- OpenAI API 兼容

---

**OrchestrationApi** - 让多 AI 服务商管理变得简单高效！ 🚀