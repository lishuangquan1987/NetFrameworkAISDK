# NetFrameworkAI

> **面向 .NET Framework 4.0+ 的 OpenAI & Anthropic SDK** — AI Agent、工具调用、MCP 客户端、技能管理。

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0+-5C2D91)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[🇬🇧 English Documentation](README.md)

NetFrameworkAI 为传统的 .NET Framework 4.0+ 项目带来了现代化的 AI 能力。它在 OpenAI 和 Anthropic API 之上提供了统一接口，并内置了自动处理多轮工具调用的 AI Agent。

---

## 特性

- **统一 API** — 通过 `IAIClient` 通用接口同时支持 OpenAI 和 Anthropic，一行代码切换后端。
- **AI Agent** — 自动工具调用循环。使用 `[Description]` 特性定义工具，Agent 自动执行并回传结果。
- **工具调用** — 完整的函数/工具调用支持，通过方法签名自动生成 JSON Schema，强类型安全。
- **流式响应** — 通过回调实时获取响应流，支持文本和工具调用的增量合并。
- **MCP 客户端** — 内置 Model Control Protocol 客户端，可连接 MCP 服务器并将其工具注入为原生 `AIFunction` 实例。
- **技能管理器** — MAF 风格的渐进式技能披露。仅在 system prompt 中注入技能摘要，通过 `load_skill` 工具调用按需加载完整内容。
- **多模态** — 文本和图片内容支持（URL 和 base64），适用于兼容模型。
- **.NET 4.0+** — 完全兼容 .NET Framework 4.0，不使用 C# 6.0+ 特性。默认启用 TLS 1.2。

---

## 快速开始

### 安装

克隆仓库并添加项目引用：

```xml
<Reference Include="NetFrameworkAI">
  <HintPath>path\to\NetFrameworkAI.dll</HintPath>
</Reference>
```

或从源码编译：

```bash
git clone https://github.com/YOUR_USERNAME/NetFrameworkAI.git
cd NetFrameworkAI
dotnet build src/NetFrameworkAI
```

依赖项（通过 NuGet 还原）：
- `Newtonsoft.Json` (≥12.0.3)
- `Flurl` (≥3.0.0)

---

## 使用示例

### 1. 基本对话（OpenAI）

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4",
    "你是一个有帮助的助手。", null);
var result = agent.Run("你好！");
Console.WriteLine(result.Result);
```

### 2. AI Agent 工具调用

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System.ComponentModel;

// 使用 [Description] 特性定义工具
[Description("获取指定位置的天气信息")]
static string GetWeather(
    [Description("城市名称，如 Beijing")] string location)
{
    return "晴天，25°C";
}

// 创建 AI Agent
var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4",
    "你是一个有帮助的助手。",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

// 运行对话 — 工具调用自动处理
var result = agent.Run("北京天气怎么样？",
    onToolCall: e =>
    {
        Console.WriteLine($"工具: {e.FunctionName}");
        Console.WriteLine($"参数: {e.FunctionArguments}");
        Console.WriteLine($"结果: {e.Result}");
    });

Console.WriteLine(result.Result);
```

### 3. Anthropic

```csharp
using NetFrameworkAISDK.Anthropic;

var client = new AnthropicClient("sk-ant-...");
var agent = new AIAgent(client, "claude-3-sonnet-20240229",
    "你是一个有帮助的助手。",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("东京天气怎么样？");
```

### 4. 流式响应

```csharp
agent.RunStreaming(
    "给我讲个故事",
    onUpdate: chunk => Console.Write(chunk),
    onError: error => Console.WriteLine($"错误: {error.Message}"));
```

### 5. MCP 工具

```csharp
var mcp = new McpClient();
mcp.Connect("path/to/mcp-server.exe");
mcp.Initialize();

var tools = mcp.ListTools();
var mcpFunctions = new List<AIFunction>();

foreach (var tool in tools.Result)
{
    mcpFunctions.Add(AIFunction.CreateFromMcpTool(
        tool.Name, tool.Description, tool.InputSchema,
        new Func<string, string>(args =>
            mcp.CallTool(tool.Name, args).Result)));
}

var agent = new AIAgent(client, "gpt-4", "...", mcpFunctions);
```

### 6. 技能（渐进式披露）

```csharp
var skills = SkillManager.DiscoverSkills("./skills");
var prompt = SkillManager.BuildProgressivePrompt(skills);

var agent = new AIAgent(client, "gpt-4", prompt, new[]
{
    SkillManager.CreateLoadSkillFunction(skills),
    SkillManager.CreateReadSkillTool(skills)
});
```

---

## 项目结构

```
src/NetFrameworkAISDK/
├── Common/               # 核心抽象
│   ├── AIAgent.cs        # Agent 循环（工具调用、流式）
│   ├── AIClientBase.cs   # 共享客户端基础设施
│   ├── AIFunction.cs     # 工具函数定义
│   ├── AIFunctionFactory.cs  # 基于反射的工具创建
│   ├── AgentTools.cs     # 内置系统工具（读写文件、搜索）
│   ├── HttpClientBase.cs # HTTP 客户端（重试、超时、TLS 1.2）
│   ├── IAIClient.cs      # 统一客户端接口
│   ├── JsonHelper.cs     # snake_case JSON 序列化
│   ├── McpClient.cs      # MCP 协议客户端
│   ├── SkillManager.cs   # 渐进式技能披露
│   └── ...               # 模型：ConversationMessage、
│                            ToolCallRequest、ApiResponse 等
├── OpenAI/               # OpenAI 实现
│   ├── OpenAIClient.cs   # OpenAI API 客户端
│   └── Models/           # ChatCompletion、ToolCall、Usage 等
└── Anthropic/            # Anthropic 实现
    ├── AnthropicClient.cs  # Anthropic API 客户端
    └── Models/           # Messages、ContentBlock、StreamEvent 等
```

---

## 环境要求

- .NET Framework 4.0 或更高版本
- Newtonsoft.Json (≥12.0.3)
- Flurl (≥3.0.0)

---

## 许可证

[MIT](LICENSE)