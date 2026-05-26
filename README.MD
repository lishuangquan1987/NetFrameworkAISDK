# NetFrameworkAI

> **OpenAI & Anthropic SDK for .NET Framework 4.0+** — AI Agent, tool calling, structured output, MCP client, skill management.

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0+-5C2D91)](https://dotnet.microsoft.com/download/dotnet-framework)
[![NuGet](https://img.shields.io/nuget/v/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[🇨🇳 中文文档](README.zh-CN.md)

NetFrameworkAI brings modern AI capabilities to legacy .NET Framework 4.0+ projects. It provides a unified interface over OpenAI and Anthropic APIs, with a built-in AI Agent that handles multi-turn tool calling, structured JSON output, and skill management automatically.

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Usage Examples](#usage-examples)
  - [Basic Chat](#1-basic-chat)
  - [AI Agent with Tool Calling](#2-ai-agent-with-tool-calling)
  - [Structured JSON Output](#3-structured-json-output)
  - [Multi-modal (Image + Text)](#4-multi-modal-image--text)
  - [Streaming](#5-streaming)
  - [Anthropic Claude](#6-anthropic-claude)
  - [Skills (Progressive Disclosure)](#7-skills-progressive-disclosure)
  - [MCP Tools](#8-mcp-tools)
- [API Overview](#api-overview)
- [Requirements](#requirements)
- [License](#license)

---

## Features

- **Unified API** — Common `IAIClient` interface for both OpenAI and Anthropic; switch providers with one line of code.
- **AI Agent** — Automated tool-calling loop. Define tools with `[Description]` attributes; the Agent handles execution and result feeding.
- **Structured JSON Output** — Call `agent.RunStructured<T>()` to get strongly-typed objects directly. Uses OpenAI native `response_format` or Anthropic tool-use hack automatically.
- **Multi-modal** — Text and image content (URL and base64) for compatible models like GPT-4o and Claude 3.5 Sonnet.
- **Tool Calling** — Full function/tool calling with auto-generated JSON Schema from method signatures.
- **Streaming** — Real-time response streaming via callbacks.
- **MCP Client** — Built-in Model Control Protocol client for connecting MCP servers and injecting their tools as native `AIFunction` instances.
- **Skill Manager** — MAF-style progressive skill disclosure. Injects only skill summaries into the system prompt; full content loads on demand via `load_skill` tool calls.
- **Built-in Agent Tools** — File read/write, code search, directory listing tools ready to use.
- **.NET 4.0+** — Fully compatible with .NET Framework 4.0, no C# 6.0+ features used. TLS 1.2 enabled by default.

---

## Installation

### Install via NuGet (recommended)

```bash
Install-Package NetFrameworkAISDK
```

Or with the .NET CLI:

```bash
dotnet add package NetFrameworkAISDK
```

### Manual build

```bash
git clone https://github.com/lishuangquan1987/NetFrameworkAISDK.git
cd NetFrameworkAISDK
msbuild NetFrameworkAISDK.sln /p:Configuration=Release
```

---

## Quick Start

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

---

## Usage Examples

### 1. Basic Chat

```csharp
var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4o", "You are a helpful assistant.", null);
var result = agent.Run("What is the capital of France?");
Console.WriteLine(result.Result);
// Output: The capital of France is Paris.
```

### 2. AI Agent with Tool Calling

Define tools with `[Description]` attributes. The Agent automatically generates JSON Schema, calls the tool when needed, and feeds the result back to the model.

```csharp
using System.ComponentModel;

[Description("Get the current weather for a location")]
static string GetWeather(
    [Description("City name, e.g. Beijing")] string location)
{
    return "Sunny, 25°C";
}

var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4o", "You are a helpful assistant.",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("What's the weather in Beijing?",
    onToolCall: e =>
    {
        Console.WriteLine($"Tool: {e.FunctionName}");
        Console.WriteLine($"Args: {e.FunctionArguments}");
        Console.WriteLine($"Result: {e.Result}");
    });

Console.WriteLine(result.Result);
// Output: The weather in Beijing is sunny, 25°C.
```

### 3. Structured JSON Output

Get strongly-typed objects directly from the AI. Define a C# class, call `RunStructured<T>()`, and get the result.

```csharp
public class WeatherInfo
{
    public string City { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }
}

var agent = AIAgent.CreateMinimal(client, "gpt-4o", "You are a weather assistant.");

var result = agent.RunStructured<WeatherInfo>("What is the weather in Beijing?");

if (result.IsSuccess)
{
    Console.WriteLine(result.Result.City);        // "Beijing"
    Console.WriteLine(result.Result.Temperature);  // 22.5
    Console.WriteLine(result.Result.Condition);    // "Sunny"
}
```

**How it works:**
- OpenAI backend: uses native `response_format` with `json_schema` and `strict: true`
- Anthropic backend: automatically injects a hidden `structured_output` tool and intercepts the response

Supports: `string`, `int`, `double`, `bool`, `DateTime`, enums, `List<T>`, `T[]`, nullable types, and nested classes.

### 4. Multi-modal (Image + Text)

Send images to GPT-4o or Claude 3.5 Sonnet using URL or base64 data.

```csharp
var contentParts = new List<MessageContent>
{
    MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "auto")
};

var result = agent.Run("What is in this image?", contentParts);
Console.WriteLine(result.Result);
```

Multiple images:

```csharp
var contentParts = new List<MessageContent>
{
    MessageContent.CreateText("Here are two images:"),
    MessageContent.CreateImageFromUrl("https://example.com/img1.jpg"),
    MessageContent.CreateImageFromBase64(base64Data, "image/jpeg")
};

var result = agent.Run("Compare these images", contentParts);
```

### 5. Streaming

```csharp
agent.RunStreaming(
    "Tell me a story",
    onUpdate: chunk => Console.Write(chunk),
    onError: error => Console.WriteLine($"Error: {error.Message}"));
```

### 6. Anthropic Claude

Switch from OpenAI to Anthropic by changing only the client and model:

```csharp
var client = new AnthropicClient("sk-ant-...");
var agent = new AIAgent(client, "claude-3-5-sonnet-20241022",
    "You are a helpful assistant.",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("What's the weather in Tokyo?");
```

### 7. Skills (Progressive Disclosure)

MAF-style skill loading — only skill summaries go into the system prompt; full content loads on demand.

```csharp
var agent = AIAgent.CreateWithDefaults(
    client, "gpt-4o",
    "You are a coding assistant.",
    "./skills",                    // skill directory (auto-discovered)
    new[] { myCustomTool });       // extra tools
```

Or manually:

```csharp
var skillManager = new SkillManager("./skills");
var prompt = skillManager.BuildProgressivePrompt();

var agent = new AIAgent(client, "gpt-4o", prompt, new[]
{
    skillManager.CreateLoadSkillFunction(),
    skillManager.CreateReadSkillTool()
});
```

### 8. MCP Tools

Connect to any MCP server and use its tools:

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

## API Overview

| Class | Namespace | Description |
|-------|-----------|-------------|
| `AIAgent` | `NetFrameworkAISDK.Common` | Main entry point. Manages conversation, tool calling, and agent loop. |
| `OpenAIClient` | `NetFrameworkAISDK.OpenAI` | OpenAI API implementation. |
| `AnthropicClient` | `NetFrameworkAISDK.Anthropic` | Anthropic API implementation. |
| `AIFunctionFactory` | `NetFrameworkAISDK.Common` | Create tools from methods via reflection. |
| `AgentTools` | `NetFrameworkAISDK.Common` | Built-in tools (read/write files, search code, list directory). |
| `SkillManager` | `NetFrameworkAISDK.Common` | Skill discovery, progressive prompt building. |
| `McpClient` | `NetFrameworkAISDK.Common` | MCP protocol client for external tool servers. |
| `JsonSchemaGenerator` | `NetFrameworkAISDK.Common` | Auto-generate JSON Schema from .NET types. |

### AIAgent Factory Methods

```csharp
// Minimal agent (no default tools, no skills)
var agent = AIAgent.CreateMinimal(client, model, instructions, extraTools);

// Agent with default tools + skills
var agent = AIAgent.CreateWithDefaults(client, model, instructions, skillsDirectory, extraTools);

// Full constructor
var agent = new AIAgent(client, model, instructions, tools, includeDefaultTools, skillsDirectory);
```

---

## Requirements

- .NET Framework 4.0 or later
- Newtonsoft.Json 13.0.1 (auto-restored by NuGet)

---

## License

[MIT](LICENSE)