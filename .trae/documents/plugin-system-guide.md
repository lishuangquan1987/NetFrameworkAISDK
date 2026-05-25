# 插件系统使用指南

## 概述

NetFrameworkAISDK 提供了一个完整的插件系统，允许开发者通过插件扩展框架的功能。本文档介绍如何使用现有的插件以及如何开发新的插件。

## 核心组件

### 1. 插件管理器 (PluginManager)

负责插件的发现、加载和管理。

```csharp
var pluginManager = new PluginManager();

// 从程序集加载插件
int count = pluginManager.LoadPluginsFromAssembly(assembly);

// 从目录加载插件
count = pluginManager.LoadPluginsFromDirectory("path/to/plugins");

// 获取插件
var plugin = pluginManager.GetPlugin("plugin-id");

// 配置插件
pluginManager.ConfigurePlugin("plugin-id", new PluginConfig
{
    IsEnabled = true,
    Settings = new Dictionary<string, object>
    {
        { "setting1", "value1" }
    }
});

// 启用/禁用插件
pluginManager.EnablePlugin("plugin-id");
pluginManager.DisablePlugin("plugin-id");
```

### 2. 中间件管道 (MiddlewarePipeline)

用于在 Agent 执行过程中拦截和修改请求/响应。

```csharp
var pipeline = new MiddlewarePipeline();

// 添加中间件（按 Order 排序）
pipeline.Use(new LoggingMiddleware(msg => Console.WriteLine(msg)))
       .Use(new CachingMiddleware(30, 100))
       .Use(new RateLimitingMiddleware(60, 1000))
       .Use(new SecurityMiddleware());

// 执行管道
var context = new AgentContext
{
    UserMessage = "Hello",
    Options = new ConversationOptions()
};

var result = pipeline.Execute(context, () =>
{
    // Agent 核心逻辑
    return agent.Run(context.UserMessage);
});
```

## 内置中间件

### 1. 日志中间件 (LoggingMiddleware)

记录 Agent 执行过程。

```csharp
var middleware = new LoggingMiddleware(
    logger: msg => Console.WriteLine(msg),
    logRequest: true,
    logResponse: true,
    logToolCalls: true
);
```

### 2. 异常处理中间件 (ExceptionHandlingMiddleware)

捕获并处理异常，支持重试。

```csharp
var middleware = new ExceptionHandlingMiddleware(
    exceptionLogger: (requestId, ex) => Log(ex),
    maxRetries: 3,
    retryDelayMs: 1000
);
```

### 3. 缓存中间件 (CachingMiddleware)

缓存响应以避免重复请求。

```csharp
var middleware = new CachingMiddleware(
    cacheExpirationMinutes: 30,
    maxCacheSize: 1000
);

// 清除缓存
middleware.ClearCache();
```

### 4. 限流中间件 (RateLimitingMiddleware)

限制请求速率。

```csharp
var middleware = new RateLimitingMiddleware(
    requestsPerMinute: 60,
    requestsPerHour: 1000
);

// 获取当前计数
int minuteCount = middleware.GetCurrentMinuteCount();
int hourCount = middleware.GetCurrentHourCount();
```

### 5. 安全中间件 (SecurityMiddleware)

过滤内容和检测 PII。

```csharp
var middleware = new SecurityMiddleware(
    enableContentFilter: true,
    enablePiiDetection: true,
    blockedPatterns: new List<string> { "badpattern" },
    securityLog: msg => Console.WriteLine(msg)
);
```

## 存储插件

### 文件存储 (FileConversationStore)

使用 JSON 文件存储对话历史。

```csharp
var store = new FileConversationStore("path/to/storage");

// 保存会话
store.SaveSession(sessionId, messages);

// 加载会话
var messages = store.LoadSession(sessionId);

// 列出所有会话
var sessions = store.ListSessions();

// 保存快照
store.SaveSnapshot(sessionId, "checkpoint-1", messages);

// 加载快照
var snapshot = store.LoadSnapshot(sessionId, "checkpoint-1");

// 删除会话
store.DeleteSession(sessionId);
```

## 工具注册表 (ToolRegistry)

管理 Agent 可用的工具。

```csharp
var registry = new ToolRegistry();

// 注册工具
registry.Register(tool);
registry.RegisterRange(tools);

// 获取工具
var tool = registry.Get("tool-name");

// 设置权限
registry.SetPermission("tool-name", new ToolPermission
{
    Level = ToolPermissionLevel.RequiresApproval,
    Description = "Requires approval"
});

// 检查权限
var permission = registry.GetPermission("tool-name");

// 检查工具是否存在
bool exists = registry.Exists("tool-name");
```

## 插件开发

### 创建自定义中间件

```csharp
public class CustomMiddleware : AgentMiddlewareBase
{
    public override string Name
    {
        get { return "Custom Middleware"; }
    }

    public override int Order
    {
        get { return 50; }
    }

    public override ApiResponse<string> Invoke(
        AgentContext context,
        Func<ApiResponse<string>> next)
    {
        // 在调用之前执行
        Console.WriteLine("Before: " + context.UserMessage);

        // 调用下一个中间件或 Agent
        var response = next();

        // 在调用之后执行
        Console.WriteLine("After: " + response.Result);

        return response;
    }
}
```

### 创建自定义存储插件

```csharp
[Plugin("MyStoragePlugin", "1.0.0")]
[StoragePlugin("MyStorage")]
public class MyStoragePlugin : IStoragePlugin
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string Website { get; set; }
    public string[] Dependencies { get; set; }
    public string StorageType { get; set; }

    public void Initialize(PluginConfig config)
    {
        // 初始化配置
    }

    public PluginValidationResult Validate()
    {
        return PluginValidationResult.Success();
    }

    public IConversationStore CreateStore(PluginConfig config)
    {
        return new MyConversationStore();
    }
}
```

## 配置示例

### 插件配置文件 (plugins.json)

```json
{
  "plugins": {
    "enabled": [
      "LoggingMiddleware",
      "CachingMiddleware",
      "RateLimitingMiddleware",
      "FileStorage"
    ],
    "settings": {
      "LoggingMiddleware": {
        "logRequest": true,
        "logResponse": true,
        "logToolCalls": true
      },
      "CachingMiddleware": {
        "cacheExpirationMinutes": 30,
        "maxCacheSize": 1000
      },
      "RateLimitingMiddleware": {
        "requestsPerMinute": 60,
        "requestsPerHour": 1000
      },
      "FileStorage": {
        "baseDirectory": "C:\\AppData\\NetFrameworkAISDK\\sessions"
      }
    }
  }
}
```

## 最佳实践

1. **中间件顺序**: 通常按以下顺序排列：
   - 安全中间件 (Order: -30)
   - 限流中间件 (Order: -20)
   - 异常处理中间件 (Order: -10)
   - 缓存中间件 (Order: 10)
   - 日志中间件 (Order: 0)

2. **错误处理**: 始终在中间件中处理异常，避免崩溃

3. **性能优化**: 
   - 缓存中间件可以显著提高性能
   - 避免在中间件中执行耗时操作

4. **安全性**:
   - 启用安全中间件过滤恶意内容
   - 敏感工具设置权限控制
   - 启用 PII 检测保护隐私

5. **测试**: 为自定义中间件编写单元测试

## 示例代码

完整的示例代码请参考 `samples/NetFrameworkAISDK.Samples/Samples/PluginSystemSample.cs`

运行示例：
```bash
cd samples/NetFrameworkAISDK.Samples
dotnet run
# 选择选项 11 - Plugin System
```

## 下一步

- 实现模型客户端插件以支持更多 AI 提供商
- 实现技能提供器插件以从更多来源加载技能
- 探索高级用法如多 Agent 协作和工作流
