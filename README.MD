# NetFrameworkAI

为 .NET Framework 4.0 及以上版本设计的 OpenAI 和 Anthropic API SDK，集成 AI Agent、工具调用和 Skills 管理。

## 功能特性

- 支持 .NET Framework 4.0 及以上版本
- 使用 Flurl 进行 HTTP 请求
- 强类型 JSON 序列化（使用 Newtonsoft.Json，snake_case 命名策略）
- 支持流式和非流式 API 调用
- AI Agent（`AIAgent`）自动处理工具调用循环
- `AIFunctionFactory.Create` 通过 `[Description]` 特性自动生成工具 Schema
- `AgentTools.CreateDefaultTools()` 通过反射自动发现 `[Description]` 标注的方法
- `SkillManager` 支持 MAF 渐进式披露（progressive disclosure）协议
- 错误处理返回错误对象而非抛出异常

## 快速开始

### 安装依赖

确保安装以下 NuGet 包：
- Newtonsoft.Json 13.0.1

### 使用示例

#### OpenAI - 基本聊天（非流式）

```csharp
using NetFrameworkAI.OpenAI;
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        var client = new OpenAIClient("your-api-key");
        
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = ChatRole.System, Content = "You are a helpful assistant." },
            new ChatMessage { Role = ChatRole.User, Content = "Hello!" }
        };
        
        var response = client.CreateChatCompletion("gpt-3.5-turbo", messages);
        
        if (response.IsSuccess)
        {
            Console.WriteLine(response.Result.Choices[0].Message.Content);
        }
        else
        {
            Console.WriteLine("Error: " + response.Error.Message);
        }
    }
}
```

#### OpenAI - 流式聊天

```csharp
using NetFrameworkAI.OpenAI;
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        var client = new OpenAIClient("your-api-key");
        
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = ChatRole.User, Content = "Tell me a joke about a pirate." }
        };
        
        Console.Write("Assistant: ");
        
        client.CreateChatCompletionStream(
            "gpt-3.5-turbo",
            messages,
            onData: streamResponse => 
            {
                if (streamResponse.Choices != null && streamResponse.Choices.Count > 0)
                {
                    var delta = streamResponse.Choices[0].Delta;
                    if (delta != null && !string.IsNullOrEmpty(delta.Content))
                    {
                        Console.Write(delta.Content);
                    }
                }
            },
            onError: error => 
            {
                Console.WriteLine("\nError: " + error.Message);
            }
        );
        
        Console.WriteLine();
    }
}
```

#### AIAgent - 工具调用（MAF 风格）

通过 `[Description]` 特性标注方法，`AIFunctionFactory.Create` 自动生成 JSON Schema：

```csharp
using NetFrameworkAI.OpenAI;
using NetFrameworkAI.Common;
using System;
using System.ComponentModel;

class Program
{
    [Description("Get the weather for a given location.")]
    static string GetWeather([Description("The location to get the weather for")] string location)
    {
        return "The weather in " + location + " is cloudy with a high of 15°C.";
    }

    [Description("Get the current time.")]
    static string GetCurrentTime()
    {
        return "Current time: " + DateTime.Now.ToString("F");
    }

    static void Main(string[] args)
    {
        var client = new OpenAIClient("your-api-key");
        
        var agent = new AIAgent(
            client,
            "gpt-3.5-turbo",
            instructions: "You are a helpful assistant that can use tools to get information.",
            tools: new[] { AIFunctionFactory.Create(new Func<string, string>(GetWeather)), AIFunctionFactory.Create(new Func<string>(GetCurrentTime)) }
        );
        
        var response = agent.Run("What's the weather like in Tokyo?");
        
        if (response.IsSuccess)
        {
            Console.WriteLine("Assistant: " + response.Result);
        }
        else
        {
            Console.WriteLine("Error: " + response.Error.Message);
        }
    }
}
```

#### AgentTools - 通用文件/搜索工具

AgentTools 提供 13 个 LLM 常用工具，通过反射自动发现：

```csharp
using NetFrameworkAI.Common;

// 一次性获取所有工具
var tools = AgentTools.CreateDefaultTools();

// tools 包含:
// - ReadFile(path)          读取文件内容
// - WriteFile(path,content) 写入文件内容
// - ListDirectory(path[,pattern]) 列出目录项
// - Grep(pattern[,path,filePattern]) 正则搜索文本
// - Glob(pattern[,path])    按模式查找文件
// - DeleteFile(path)        删除文件
// - MakeDirectory(path)     创建目录
// - RenameFile(oldPath,newName) 重命名文件/目录
// - GetFileInfo(path)       获取文件信息
// - CopyFile(source,dest)   复制文件
// - MoveFile(source,dest)   移动文件
// - GetEnvironmentVariable(name) 获取环境变量
// - RunCommand(command[,workingDir]) 执行命令

// 每个工具的函数名 = C# 方法名（PascalCase），描述来自 [Description] 特性
```

**新增工具只需添加带 `[Description]` 的 private static 方法：**

```csharp
public static class AgentTools
{
    [Description("Read the contents of a file at the given path")]
    private static string ReadFile([Description("Absolute or relative path to the file")] string path)
    {
        // 实现...
    }

    // CreateDefaultTools() 自动通过反射发现此方法
}
```

#### SkillManager - MAF 渐进式披露

Skills 是文档驱动的指令集（SKILL.md），通过渐进式披露协议注入 LLM：

```csharp
using NetFrameworkAI.Common;

// 1. 发现 skills
var skills = SkillManager.DiscoverSkills("./skills");

// 2. 构建目录 prompt（只含 name + description，不含完整正文）
string prompt = SkillManager.BuildProgressivePrompt(skills);

// 3. 创建 load_skill 工具让 LLM 按需加载
var loadSkill = SkillManager.CreateLoadSkillFunction(skills);

// 4. 和文件工具一起注入 AIAgent
var agentTools = AgentTools.CreateDefaultTools();
agentTools.Add(loadSkill);
```

#### Anthropic - 基本消息（非流式）

```csharp
using NetFrameworkAI.Anthropic;
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        var client = new AnthropicClient("your-api-key");
        
        var messages = new List<AnthropicMessage>
        {
            new AnthropicMessage { Role = AnthropicRole.User, Content = "Hello!" }
        };
        
        var response = client.CreateMessage(
            "claude-3-haiku-20240307",
            messages,
            maxTokens: 1024,
            system: "You are a helpful assistant."
        );
        
        if (response.IsSuccess)
        {
            foreach (var content in response.Result.Content)
            {
                Console.Write(content.Text);
            }
        }
        else
        {
            Console.WriteLine("Error: " + response.Error.Message);
        }
    }
}
```

#### Anthropic - 流式消息

```csharp
using NetFrameworkAI.Anthropic;
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        var client = new AnthropicClient("your-api-key");
        
        var messages = new List<AnthropicMessage>
        {
            new AnthropicMessage { Role = AnthropicRole.User, Content = "Tell me a joke about a pirate." }
        };
        
        Console.Write("Assistant: ");
        
        client.CreateMessageStream(
            "claude-3-haiku-20240307",
            messages,
            maxTokens: 1024,
            onEvent: streamEvent => 
            {
                if (streamEvent.Type == StreamEventType.ContentBlockDelta &&
                    streamEvent.Delta != null &&
                    !string.IsNullOrEmpty(streamEvent.Delta.Text))
                {
                    Console.Write(streamEvent.Delta.Text);
                }
            },
            onError: error => 
            {
                Console.WriteLine("\nError: " + error.Message);
            }
        );
        
        Console.WriteLine();
    }
}
```

## 项目结构

```
NetFrameworkAI/
└── Common/
    ├── ApiError.cs           # 错误对象基类
    ├── JsonHelper.cs         # JSON 序列化辅助类（snake_case + NullValueHandling.Ignore）
    ├── HttpClientBase.cs     # HTTP 客户端基类（TLS 1.2 + Flurl URL）
    ├── AIFunction.cs         # AI 函数定义 + AIFunctionFactory（Create/McpTool/反射）
    ├── AIAgent.cs            # AI Agent：自动处理工具调用循环
    ├── AgentTools.cs         # Agent 通用工具（文件读/写/目录/搜索，反射自动发现）
    └── SkillManager.cs       # Skills 管理器（MAF 渐进式披露：发现→目录→按需加载）
OpenAI/
    ├── ChatMessage.cs        # 聊天消息模型
    ├── ChatCompletion.cs     # 聊天完成请求/响应模型
    └── OpenAIClient.cs       # OpenAI API 客户端
Anthropic/
    ├── AnthropicMessage.cs   # Anthropic 消息模型
    ├── Messages.cs           # 消息创建请求/响应模型
    └── AnthropicClient.cs    # Anthropic API 客户端
```

## 核心设计模式

### 1. JSON 序列化：snake_case

所有请求/响应 JSON 使用 snake_case 命名策略，以匹配 OpenAI / Anthropic API。

```csharp
new JsonSerializerSettings
{
    ContractResolver = new DefaultContractResolver
    {
        NamingStrategy = new SnakeCaseNamingStrategy()
    },
    NullValueHandling = NullValueHandling.Ignore
};
```

### 2. 工具 Schema 自动生成

通过 `[Description]` 特性标注方法和参数，`AIFunctionFactory.Create(Delegate)` 反射自动构建 JSON Schema：

```csharp
[AIFunctionFactory.Create(new Func<string, string>(MyMethod))]
// 自动生成：{ name: "MyMethod", description: "来自 [Description]", parameters: { type: "object", properties: { ... } } }
```

### 3. AgentTools 反射自动发现

`AgentTools.CreateDefaultTools()` 通过反射扫描类中所有带 `[Description]` 特性的 private static 方法，自动注册为 AI 工具。**新增工具只需添加带 `[Description]` 的方法，无需修改注册代码。**

### 4. MAF 渐进式披露（Skills）

Skills 使用 Microsoft Agent Framework 的渐进式披露协议：

1. **Advertise**：system prompt 只放 skill 目录（XML 格式的 name + description）
2. **Load**：提供 `load_skill(name)` 函数工具，LLM 按需调用获取完整正文
3. **Read**：提供文件工具，LLM 可读取 skill 目录下的资源文件
4. **Execute**：LLM 根据加载的指令执行任务

## API 设计说明

- API 密钥通过构造函数传入，SDK 不负责读取配置
- 流式 API 使用 `Action<T>` 回调方式实现（兼容 .NET 4.0）
- 所有 API 调用返回 `ApiResponse<T>` 对象，包含结果或错误信息
- 遵循 C# 4.0 / .NET Framework 4.0 兼容性（不使用 `?.`、`??`、`$""`、表达式体成员等 C# 6.0+ 特性）
- 遵循 ProjectRules.md 中定义的编码规范

## 开源协议

MIT License