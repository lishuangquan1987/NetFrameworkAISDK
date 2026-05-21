# NetFrameworkAI

> **OpenAI & Anthropic SDK for .NET Framework 4.0+** — AI Agent, tool calling, MCP client, skill management.

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0+-5C2D91)](https://dotnet.microsoft.com/download/dotnet-framework)
[![NuGet](https://img.shields.io/nuget/v/NetFrameworkAISDK)](https://www.nuget.org/packages/NetFrameworkAISDK)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[🇨🇳 中文文档](README.zh-CN.md)

NetFrameworkAI brings modern AI capabilities to legacy .NET Framework 4.0+ projects. It provides a unified interface over OpenAI and Anthropic APIs, with a built-in AI Agent that handles multi-turn tool calling automatically.

---

## Features

- **Unified API** — Common `IAIClient` interface for both OpenAI and Anthropic; switch providers with one line of code.
- **AI Agent** — Automated tool-calling loop. Define tools with `[Description]` attributes; the Agent handles execution and result feeding.
- **Tool Calling** — Full support for function/tool calling with strong typing and auto-generated JSON Schema from method signatures.
- **Streaming** — Real-time response streaming via callbacks, with incremental delta merging for both text and tool calls.
- **MCP Client** — Built-in Model Control Protocol client for connecting MCP servers and injecting their tools as native `AIFunction` instances.
- **Skill Manager** — MAF-style progressive skill disclosure. Injects only skill summaries into the system prompt; full content loads on demand via `load_skill` tool calls.
- **Multi-modal** — Text and image content support (URL and base64) for compatible models.
- **.NET 4.0+** — Fully compatible with .NET Framework 4.0, no C# 6.0+ features used. TLS 1.2 enabled by default.

---

## Quick Start

### Install

Clone the repo and add a project reference:

```xml
<Reference Include="NetFrameworkAI">
  <HintPath>path\to\NetFrameworkAI.dll</HintPath>
</Reference>
```

Or build from source:

```bash
git clone https://github.com/YOUR_USERNAME/NetFrameworkAI.git
cd NetFrameworkAI
dotnet build src/NetFrameworkAI
```

Dependencies (restored via NuGet):
- `Newtonsoft.Json` (≥12.0.3)
- `Flurl` (≥3.0.0)

---

## Usage

### 1. Basic Chat (OpenAI)

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4",
    "You are a helpful assistant.", null);
var result = agent.Run("Hello!");
Console.WriteLine(result.Result);
```

### 2. AI Agent with Tool Calling

```csharp
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System.ComponentModel;

// Define tools with [Description] attributes
[Description("Get the current weather for a location")]
static string GetWeather(
    [Description("City name, e.g. Beijing")] string location)
{
    return "Sunny, 25°C";
}

// Create AI Agent
var client = new OpenAIClient("sk-...");
var agent = new AIAgent(client, "gpt-4",
    "You are a helpful assistant.",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

// Run conversation — tool calls are handled automatically
var result = agent.Run("What's the weather in Beijing?",
    onToolCall: e =>
    {
        Console.WriteLine($"Tool: {e.FunctionName}");
        Console.WriteLine($"Args: {e.FunctionArguments}");
        Console.WriteLine($"Result: {e.Result}");
    });

Console.WriteLine(result.Result);
```

### 3. Anthropic

```csharp
using NetFrameworkAISDK.Anthropic;

var client = new AnthropicClient("sk-ant-...");
var agent = new AIAgent(client, "claude-3-sonnet-20240229",
    "You are a helpful assistant.",
    new[] { AIFunctionFactory.Create(
        new Func<string, string>(GetWeather)) });

var result = agent.Run("What's the weather in Tokyo?");
```

### 4. Streaming

```csharp
agent.RunStreaming(
    "Tell me a story",
    onUpdate: chunk => Console.Write(chunk),
    onError: error => Console.WriteLine($"Error: {error.Message}"));
```

### 5. MCP Tools

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

### 6. Skills (Progressive Disclosure)

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

## Project Structure

```
src/NetFrameworkAISDK/
├── Common/               # Core abstractions
│   ├── AIAgent.cs        # Agent loop (tool calling, streaming)
│   ├── AIClientBase.cs   # Shared client infrastructure
│   ├── AIFunction.cs     # Tool function definition
│   ├── AIFunctionFactory.cs  # Reflection-based tool creation
│   ├── AgentTools.cs     # Built-in system tools (read/write/grep)
│   ├── HttpClientBase.cs # HTTP with retry, timeout, TLS 1.2
│   ├── IAIClient.cs      # Unified client interface
│   ├── JsonHelper.cs     # snake_case JSON serialization
│   ├── McpClient.cs      # MCP protocol client
│   ├── SkillManager.cs   # Progressive skill disclosure
│   └── ...               # Models: ConversationMessage,
│                            ToolCallRequest, ApiResponse, etc.
├── OpenAI/               # OpenAI implementation
│   ├── OpenAIClient.cs   # OpenAI API client
│   └── Models/           # ChatCompletion, ToolCall, Usage, etc.
└── Anthropic/            # Anthropic implementation
    ├── AnthropicClient.cs  # Anthropic API client
    └── Models/           # Messages, ContentBlock, StreamEvent, etc.
```

---

## Requirements

- .NET Framework 4.0 or later
- Newtonsoft.Json (≥12.0.3)
- Flurl (≥3.0.0)

---

## License

[MIT](LICENSE)