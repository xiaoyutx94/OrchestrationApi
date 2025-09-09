# 使用官方 .NET 9 运行时作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# 设置时区环境变量（关键修改）
ENV TZ=Asia/Shanghai

# 使用 .NET 9 SDK 进行构建
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖
COPY OrchestrationApi.csproj .
RUN dotnet restore "OrchestrationApi.csproj"

# 复制所有源代码
COPY . .

# 构建应用
RUN dotnet build "OrchestrationApi.csproj" -c Release -o /app/build

# 发布应用
FROM build AS publish
RUN dotnet publish "OrchestrationApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 最终运行时镜像
FROM base AS final
WORKDIR /app

# 复制发布的应用
COPY --from=publish /app/publish .

# 创建必要的目录并设置权限
RUN mkdir -p /app/data /app/logs && \
    chown -R app:app /app && \
    chmod -R 755 /app/data /app/logs

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# 设置 .NET 配置环境变量支持
# 这些环境变量将会覆盖 appsettings.json 中的配置
ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# 默认配置 - 可以在 docker-compose.yml 中覆盖
ENV OrchestrationApi__Server__Host=0.0.0.0
ENV OrchestrationApi__Server__Port=5000
ENV OrchestrationApi__Database__Type=sqlite
ENV OrchestrationApi__Database__ConnectionString="Data Source=/app/data/orchestration_api.db"

# 以root身份运行以处理volume权限
USER root

# 启动应用
ENTRYPOINT ["sh", "-c", "mkdir -p /app/data /app/logs && chown -R app:app /app/data /app/logs && chmod -R 755 /app/data /app/logs && su app -s /bin/bash -c 'cd /app && dotnet OrchestrationApi.dll'"]
