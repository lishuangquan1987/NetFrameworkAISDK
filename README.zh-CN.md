# NetFrameworkAISDK

> **面向 .NET Framework 4.0+ / .NET Standard 2.0 的 OpenAI & Anthropic SDK** — AI Agent、工具调用、结构化输出、MCP 客户端、技能管理。

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0+-5C2D91)](https://dotnet.microsoft.com/download/dotnet-framework)
[![NuGet](https://img.shields.io/nuget/v/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[🇬🇧 English Documentation](README.md)

NetFrameworkAISDK 为传统的 .NET Framework 4.0+ 和 .NET Standard 2.0 项目带来了现代化的 AI 能力。它在 OpenAI 和 Anthropic API 之上提供了统一接口，并内置了自动处理多轮工具调用、结构化 JSON 输出、技能管理的 AI Agent。

---

## 目录

- [特性](#特性)
- [安装](#安装)
- [快速开始](#快速开始)
- [使用示例](#使用示例)
  - [基本对话](#1-基本对话)
  - [AI Agent 工具调用](#2-ai-agent-工具调用)
  - [结构化 JSON 输出](#3-结构化-json-输出)
  - [多模态（图片+文字）](#4-多模态图片文字)
  - [流式响应](#5-流式响应)
  - [Anthropic Claude](#6-anthropic-claude)
  - [技能（渐进式披露）](#7-技能渐进式披露)
  - [MCP 工具](#8-mcp-工具)
- [API 概览](#api-概览)
- [环境要求](#环境要求)
- [许可证](#许可证)

---

## 特性

- **统一 API** — 通过 `IAIClient` 通用接口同时支持 OpenAI 和 Anthropic，一行代码切换后端。
- **AI Agent** — 自动工具调用循环。使用 `[Description]` 特性定义工具，Agent 自动执行并回传结果。
- **结构化 JSON 输出** — 调用 `agent.RunStructured<T>()` 直接获取强类型对象。OpenAI 使用原生 `response_format`，Anthropic 使用 tool-use hack，全自动适配。
- **多模态** — 文本和图片内容支持（URL 和 base64），适用于 GPT-4o、Claude 3.5 Sonnet 等模型。
- **工具调用** — 完整的函数/工具调用支持，通过方法签名自动生成 JSON Schema，强类型安全。
- **流式响应** — 通过回调实时获取响应流。
- **MCP 客户端** — 内置 Model Control Protocol 客户端，可连接 MCP 服务器并将其工具注入为原生 `AIFunction` 实例。
- **技能管理器** — MAF 风格的渐进式技能披露。仅在 system prompt 中注入技能摘要，通过 `load_skill` 工具调用按需加载完整内容。
- **内置 Agent 工具** — 文件读写、代码搜索、目录列举等工具开箱即用。
- **.NET 4.0+ / .NET Standard 2.0** — 兼容 .NET Framework 4.0+ 和 .NET Standard 2.0，不使用 C# 6.0+ 特性。默认启用 TLS 1.2。

---

## 安装

### 通过 NuGet 安装（推荐）

```bash
Install-Package NetFrameworkAISDK
```

或使用 .NET CLI：

```bash
dotnet add package NetFrameworkAISDK
```

### 手动编译

```bash
git clone https://github.com/lishuangquan1987/NetFrameworkAISDK.git
cd NetFrameworkAISDK
msbuild NetFrameworkAISDK.sln /p:Configuration=Release
```

---

## 快速开始

### OpenAI

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

var client = new OpenAIClient("sk-...");
var agent = AIAgent.CreateMinimal(client, "gpt-4o", "你是一个有帮助的助手。");
var result = agent.Run("你好！");
Console.WriteLine(result.Result);
```

### Anthropic

```csharp
using NetFrameworkAISDK.Anthropic;

var client = new AnthropicClient("sk-ant-...");
var agent = AIAgent.CreateMinimal(client, "claude-3-5-sonnet-20241022", "你是一个有帮助的助手。");
var result = agent.Run("你好！");
Console.WriteLine(result.Result);
```

---

## 使用示例

### 1. 基本对话

```csharp
var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4o", "你是一个有帮助的助手。", null);
var result = agent.Run("法国的首都是哪里？");
Console.WriteLine(result.Result);
// 输出：法国的首都是巴黎。
```

### 2. AI Agent 工具调用

使用 `[Description]` 特性定义工具，Agent 自动生成 JSON Schema，需要时调用工具并将结果回传给模型。

```csharp
using System.ComponentModel;

[Description("获取指定位置的天气信息")]
static string GetWeather(
    [Description("城市名称，如 Beijing")] string location)
{
    return "晴天，25°C";
}

var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4o", "你是一个有帮助的助手。",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("北京天气怎么样？",
    onToolCall: e =>
    {
        Console.WriteLine($"工具: {e.FunctionName}");
        Console.WriteLine($"参数: {e.FunctionArguments}");
        Console.WriteLine($"结果: {e.Result}");
    });

Console.WriteLine(result.Result);
// 输出：北京的天气是晴天，25°C。
```

### 3. 结构化 JSON 输出

定义 C# 类，调用 `RunStructured<T>()`，直接获取强类型对象，无需手动解析 JSON。

```csharp
public class WeatherInfo
{
    public string City { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }
}

var agent = AIAgent.CreateMinimal(client, "gpt-4o", "你是一个天气助手。");

var result = agent.RunStructured<WeatherInfo>("北京的天气怎么样？");

if (result.IsSuccess)
{
    Console.WriteLine(result.Result.City);         // "Beijing"
    Console.WriteLine(result.Result.Temperature);  // 22.5
    Console.WriteLine(result.Result.Condition);    // "Sunny"
}
```

**工作原理：**
- OpenAI 后端：使用原生 `response_format` + `json_schema` + `strict: true`
- Anthropic 后端：自动注入 `structured_output` 工具并拦截响应

支持类型：`string`、`int`、`double`、`bool`、`DateTime`、枚举、`List<T>`、`T[]`、可空类型、嵌套类。

### 4. 多模态（图片+文字）

向 GPT-4o 或 Claude 3.5 Sonnet 发送图片，支持 URL 和 base64 两种方式。

```csharp
var contentParts = new List<MessageContent>
{
    MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "auto")
};

var result = agent.Run("这张图片里有什么？", contentParts);
Console.WriteLine(result.Result);
```

多张图片：

```csharp
var contentParts = new List<MessageContent>
{
    MessageContent.CreateText("这里有两张图片："),
    MessageContent.CreateImageFromUrl("https://example.com/img1.jpg"),
    MessageContent.CreateImageFromBase64(base64Data, "image/jpeg")
};

var result = agent.Run("比较这两张图片", contentParts);
```

### 5. 流式响应

```csharp
agent.RunStreaming(
    "给我讲个故事",
    onUpdate: chunk => Console.Write(chunk),
    onError: error => Console.WriteLine($"错误: {error.Message}"));
```

### 6. Anthropic Claude

只需更换客户端和模型名称，即可从 OpenAI 切换到 Anthropic：

```csharp
var client = new AnthropicClient("sk-ant-...");
var agent = new AIAgent(client, "claude-3-5-sonnet-20241022",
    "你是一个有帮助的助手。",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("东京天气怎么样？");
```

### 7. 技能（渐进式披露）

MAF 风格的技能加载 —— 只有技能摘要进入 system prompt，完整内容按需加载。

```csharp
var agent = AIAgent.CreateWithDefaults(
    client, "gpt-4o",
    "你是一个编程助手。",
    new string[] { "./skills" },   // 技能目录数组（自动发现）
    new[] { myCustomTool });       // 额外工具
```

或手动方式：

```csharp
var sm = new SkillManager("./skills");
var prompt = sm.BuildProgressivePrompt();

var agent = new AIAgent(client, "gpt-4o", prompt, new[]
{
    sm.CreateLoadSkillFunction(),
    sm.CreateReadSkillTool()
});
```

### 8. MCP 工具

连接任何 MCP 服务器并使用其工具：

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

var agent = new AIAgent(client, "gpt-4o", "...", mcpFunctions);
```

---

## API 概览

| 类 | 命名空间 | 描述 |
|-----|----------|------|
| `AIAgent` | `NetFrameworkAISDK.Common` | 主入口。管理对话、工具调用和 Agent 循环。 |
| `OpenAIClient` | `NetFrameworkAISDK.OpenAI` | OpenAI API 实现。 |
| `AnthropicClient` | `NetFrameworkAISDK.Anthropic` | Anthropic API 实现。 |
| `AIFunctionFactory` | `NetFrameworkAISDK.Common` | 通过反射从方法创建工具。 |
| `AgentTools` | `NetFrameworkAISDK.Common` | 内置工具（文件读写、代码搜索、目录列举）。 |
| `SkillManager` | `NetFrameworkAISDK.Common` | 技能发现、渐进式提示词构建。 |
| `McpClient` | `NetFrameworkAISDK.Common` | MCP 协议客户端。 |
| `JsonSchemaGenerator` | `NetFrameworkAISDK.Common` | 从 .NET 类型自动生成 JSON Schema。 |

### AIAgent 工厂方法

```csharp
// 最小化 Agent（无默认工具、无技能）
var agent = AIAgent.CreateMinimal(client, model, instructions, extraTools);

// 带默认工具+技能的 Agent
var agent = AIAgent.CreateWithDefaults(client, model, instructions, skillsDirectories, extraTools);

// 完整构造函数
var agent = new AIAgent(client, model, instructions, tools, includeDefaultTools, skillsDirectories);
```

---

## 环境要求

- .NET Framework 4.0 或更高版本，或 .NET Standard 2.0 兼容运行时（如 .NET Core 2.0+）
- Newtonsoft.Json 13.0.1（NuGet 自动还原）

---

## 许可证

[MIT](LICENSE)