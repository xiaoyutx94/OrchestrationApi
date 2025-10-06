# OrchestrationApi

一个基于 .NET 9 的企业级多服务商 AI API 代理服务，提供统一的 OpenAI 兼容接口，支持智能路由、故障转移、负载均衡等企业级功能。

## 🚀 核心特性

- **多服务商支持**: OpenAI、Anthropic Claude、Google Gemini
- **智能路由**: 基于模型、负载、可用性自动选择最佳提供商
- **故障转移**: 自动重试和Provider切换机制
- **统一接口**: 完全兼容 OpenAI API 格式，无缝替换
- **Web管理界面**: 现代化的管理仪表板
- **高性能**: 基于 .NET 9 异步架构，支持高并发
- **Docker支持**: 完整的容器化部署方案

## 📋 系统要求

- .NET 9 SDK 或 Docker
- SQLite（默认）或 MySQL 数据库
- 支持的操作系统：Windows、Linux、macOS

## 🚀 快速开始

### 使用 Docker Compose（推荐）

1. **克隆项目**
```bash
git clone https://github.com/xiaoyutx94/OrchestrationApi
cd OrchestrationApi
```

2. **启动服务**
```bash
# 使用 Docker Compose 启动服务
docker-compose up -d

# 查看服务状态
docker-compose logs -f orchestration-api
```

### 本地开发

1. **克隆项目**
```bash
git clone https://github.com/xiaoyutx94/OrchestrationApi
cd OrchestrationApi
```

2. **安装依赖并运行**
```bash
# 安装依赖
dotnet restore

# 运行服务
dotnet run

# 或使用热重载（开发模式）
dotnet watch run
```

## 🔧 访问服务

服务启动后（默认端口5000）：

- **管理仪表板**: http://localhost:5000/dashboard
- **API文档**: http://localhost:5000/swagger
- **健康检查**: http://localhost:5000/health

默认登录凭据：
- 用户名：`admin`
- 密码：`admin123`

## 📖 基本使用

### OpenAI 兼容 API

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer your-proxy-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-3.5-turbo",
    "messages": [
      {"role": "user", "content": "Hello, world!"}
    ]
  }'
```

### 获取可用模型

```bash
curl -X GET http://localhost:5000/v1/models \
  -H "Authorization: Bearer your-proxy-key"
```

## 🐳 Docker 部署

### 基础部署（SQLite）

```yaml
version: '3.8'
services:
  orchestration-api:
    image: ghcr.io/xiaoyutx94/orchestrationapi:latest
    container_name: orchestration-api
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - OrchestrationApi__Auth__Password=your-secure-password
    volumes:
      - ./data:/app/data
      - ./logs:/app/logs
    restart: unless-stopped
```

### 生产环境（MySQL）

```yaml
version: '3.8'
services:
  orchestration-api:
    image: ghcr.io/xiaoyutx94/orchestrationapi:latest
    container_name: orchestration-api
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - OrchestrationApi__Database__Type=mysql
      - OrchestrationApi__Database__MySqlConnectionString=Server=mysql;Database=orchestration_api;Uid=orchestration;Pwd=secure_password;
      - OrchestrationApi__Auth__Password=your-secure-password
    depends_on:
      - mysql
    restart: unless-stopped

  mysql:
    image: mysql:8.0
    container_name: orchestration-mysql
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

## 🔧 开发调试

### 本地开发环境

```bash
# 恢复依赖
dotnet restore

# 编译项目
dotnet build

# 运行（开发模式）
dotnet run --environment Development

# 热重载模式
dotnet watch run
```

### 调试配置

开发环境默认配置：
- 数据库：SQLite（自动创建）
- 日志级别：Information
- Swagger UI：启用
- 热重载：支持

## 📞 支持

- **问题报告**: 通过 GitHub Issues 提交
- **功能建议**: 通过 Issues 提出新功能需求

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。