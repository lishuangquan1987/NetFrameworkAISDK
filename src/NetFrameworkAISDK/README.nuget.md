# NetFrameworkAISDK SDK

**OpenAI & Anthropic SDK for .NET Framework 4.0+ / .NET Standard 2.0** — AI Agent, tool calling, structured output, MCP client, TLS proxy, skill management.

[![NuGet](https://img.shields.io/nuget/v/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)

***

## Quick Start

### Install

```
Install-Package NetFrameworkAISDK
```

### OpenAI

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

var client = new OpenAIClient("sk-...");
var agent = AIAgent.CreateMinimal(client, "gpt-4o", "You are a helpful assistant.");
var result = agent.Run("Hello!");
Console.WriteLine(result.Result);
```

### Anthropic

```csharp
using NetFrameworkAISDK.Anthropic;

var client = new AnthropicClient("sk-ant-...");
var agent = AIAgent.CreateMinimal(client, "claude-3-5-sonnet-20241022", "You are a helpful assistant.");
var result = agent.Run("Hello!");
Console.WriteLine(result.Result);
```

***

## Key Features

### AI Agent with Tool Calling

Define tools with `[Description]` attributes. The Agent automatically handles multi-turn tool calling.

```csharp
[Description("Get the current weather")]
static string GetWeather([Description("City name")] string location) => "Sunny, 25C";

var agent = new AIAgent(client, "gpt-4o", "You are helpful.",
    new[] { AIFunctionFactory.Create(new Func<string, string>(GetWeather)) });

var result = agent.Run("What's the weather in Beijing?");
```

### Structured JSON Output

Get strongly-typed objects directly from the AI.

```csharp
public class WeatherInfo
{
    public string City { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }
}

var result = agent.RunStructured<WeatherInfo>("What is the weather in Beijing?");

if (result.IsSuccess)
{
    Console.WriteLine(result.Result.City);        // "Beijing"
    Console.WriteLine(result.Result.Temperature);  // 22.5
}
```

- OpenAI: uses native `response_format` (json\_schema + strict mode)
- Anthropic: automatically injects a `structured_output` tool

### Multi-modal (Image + Text)

```csharp
var parts = new List<MessageContent>
{
    MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "auto")
};
var result = agent.Run("What is in this image?", parts);
```

### Streaming

```csharp
agent.RunStreaming("Tell me a story",
    onUpdate: chunk => Console.Write(chunk),
    onError: err => Console.WriteLine(err.Message));
```

### Skills (Progressive Disclosure)

```csharp
var agent = AIAgent.CreateWithDefaults(
    client, "gpt-4o", "You are a coding assistant.",
    new[] { "./skills" }, new[] { myCustomTool });
```

### MCP Tools

```csharp
var mcp = new McpClient();
mcp.Connect("path/to/mcp-server.exe");
mcp.Initialize();

// 一行注入全部 MCP 工具
var functions = mcp.ListAsAIFunctions();
var agent = new AIAgent(client, "gpt-4o", "...", functions.Result);
```

### Thinking / Reasoning Content

```csharp
agent.RunStreaming("Explain relativity",
    onUpdate: chunk => Console.Write(chunk),
    onError: err => Console.WriteLine(err.Message),
    onReasoning: thinking => Console.Write("[think] " + thinking));
```

### TLS Proxy (Windows XP compatible)

```csharp
// XP 自动启用，非 XP 诊断用强制：
HttpClientBase.ForceTlsProxyForDiagnostics();
```

***

## Project Links

- GitHub: <https://github.com/lishuangquan1987/NetFrameworkAISDK>
- Report issues: <https://github.com/lishuangquan1987/NetFrameworkAISDK/issues>
- Full documentation: [https://github.com/lishuangquan1987/NetFrameworkAISDK/blob/master/README.MD](https://github.com/lishuangquan1987/NetFrameworkAISDK/blob/master/README.md)

***

## Requirements

- .NET Framework 4.0 or later, or .NET Standard 2.0 compatible runtime (e.g. .NET Core 2.0+)
- Newtonsoft.Json 13.0.1 (auto-restored)
- BouncyCastle 1.8.9 (auto-restored, used for TLS 1.2 proxy on legacy Windows)

