# 重构计划：AIAgent 支持多模态（图片）输入

## 调研总结：开源项目如何处理多模态

### 1. Microsoft Agent Framework (MAF) - .NET
```csharp
// MAF 使用 ChatMessage 的多内容块模式
ChatMessage userMessage = new ChatMessage(ChatRole.User, [
    new TextContent("What is in this image?"),
    new DataContent(imageBytes, "image/png")
]);
await agent.RunAsync(userMessage, thread);
```
- **模式**：通过 `ChatMessage` 的 `IList<AIContent>` 内容列表，混排文本和图片
- `TextContent` 表示文本，`DataContent` 表示二进制内容（图片等）
- Agent 的 `RunAsync` 接收 `ChatMessage`（不是裸 string）

### 2. OpenAI Agents SDK (Python)
```python
# OpenAI Agents SDK 接受 string 或多内容列表
result = await Runner.run(agent, [
    {"type": "text", "text": "What is in this image?"},
    {"type": "image_url", "image_url": {"url": "https://..."}}
])
```
- **模式**：`Runner.run()` 的 `input` 参数可以是 `string` 或 `list[dict]`
- 图片支持两种：`image_url`（URL）和 base64 data URI

### 3. Claude Agent SDK (Python)
```python
# Claude Agent SDK 使用标准 Messages API 格式
await client.query(prompt=[
    {"type": "text", "text": "Describe this image"},
    {"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": "..."}}
])
```
- **模式**：内容块数组，`type: "image"` + `source` 对象包含 base64 数据

### 关键结论
**三个框架的共同模式：**
1. Agent 入口方法接受"文本+内容块"的组合，而非纯文本
2. 图片以 URL 或 base64 两种方式传入
3. 内容块格式：`{ type: "image", ... }` + 图片数据
4. 不改变 Agent 循环逻辑 —— 多模态只是在消息构造层面追加内容块

---

## 当前状态分析

### 已具备的基础设施（好消息）

| 组件 | 状态 | 说明 |
|------|------|------|
| `MessageContent` 类 | ✅ 已有 | 包含 `Type`, `Text`, `ImageUrl`, `ImageBase64`, `MediaType`, `Detail` |
| `ContentType.Text` / `ContentType.Image` | ✅ 已有 | 内容类型常量 |
| `ConversationMessage.ContentParts` | ✅ 已有 | `List<MessageContent>` 字段 |
| `ImageContentPart` / `ImageDetail` | ✅ 已有 | OpenAI 格式的图像传输类 |
| `ContentBlock.Source` / `ContentBlock.MediaType` | ✅ 已有 | Anthropic 格式的图像传输类 |
| `OpenAIClient` 多模态转换 | ✅ 已有 | `ConvertToOpenAiMessages()` 已有 ContentParts 处理逻辑 |
| `AnthropicClient` 多模态转换 | ✅ 已有 | 已有 ContentParts 处理逻辑 |

### 缺失的部分（需要添加）

| 缺失项 | 说明 |
|--------|------|
| `AIAgent.Run()` 无图片入口 | 只接受 `string userMessage`，无法传入图片 |
| `AIAgent.RunStreaming()` 无图片入口 | 同上 |
| `MessageContent` 无便捷创建方法 | 用户需手动构造对象，缺少工厂方法 |

### 当前无法实现的场景
```csharp
// ❌ 目前无法做到：传图片给 Agent
var agent = new AIAgent(client, model, instructions, tools);
agent.Run("What is in this image?"); // 只能传文本，无法传图片
```

---

## 重构方案

### 设计原则

1. **最小改动**：只添加入口方法，不修改 AgentLoop / StreamingLoop
2. **后端已就绪**：OpenAI 和 Anthropic 客户端已正确处理 ContentParts
3. **向后兼容**：现有 `Run(string)` / `RunStreaming(string)` 不变
4. **MAF 风格**：参考 MAF 的多内容块模式

### 具体步骤

#### 步骤 1：给 `MessageContent` 添加便捷工厂方法

```csharp
// 在 MessageContent.cs 中添加静态工厂方法

/// <summary>从 URL 创建图像内容块</summary>
public static MessageContent CreateImageFromUrl(string imageUrl, string detail = null)
{
    return new MessageContent
    {
        Type = ContentType.Image,
        ImageUrl = imageUrl,
        Detail = detail
    };
}

/// <summary>从 base64 数据创建图像内容块</summary>
public static MessageContent CreateImageFromBase64(string base64Data, string mediaType = "image/png")
{
    return new MessageContent
    {
        Type = ContentType.Image,
        ImageBase64 = base64Data,
        MediaType = mediaType
    };
}

/// <summary>创建文本内容块</summary>
public static MessageContent CreateText(string text)
{
    return new MessageContent
    {
        Type = ContentType.Text,
        Text = text
    };
}
```

#### 步骤 2：给 `AIAgent` 添加多模态 Run() 重载

```csharp
/// <summary>
/// 执行一次非流式多模态对话，支持文本+图片输入
/// </summary>
/// <param name="userMessage">用户文本消息</param>
/// <param name="contentParts">多模态内容块列表（图片等），可为 null</param>
/// <param name="onToolCall">工具调用回调（可选）</param>
public ApiResponse<string> Run(string userMessage, List<MessageContent> contentParts, Action<ToolCallEventArgs> onToolCall = null)
{
    _conversationHistory.Add(new ConversationMessage
    {
        Role = MessageRole.User,
        Content = userMessage,
        ContentParts = contentParts
    });

    return AgentLoop(onToolCall, DefaultMaxIterations);
}
```

#### 步骤 3：给 `AIAgent` 添加多模态 RunStreaming() 重载

```csharp
/// <summary>
/// 执行流式多模态对话，支持文本+图片输入
/// </summary>
public void RunStreaming(
    string userMessage,
    List<MessageContent> contentParts,
    Action<string> onUpdate,
    Action<ApiError> onError,
    Action<ToolCallEventArgs> onToolCall = null)
{
    _conversationHistory.Add(new ConversationMessage
    {
        Role = MessageRole.User,
        Content = userMessage,
        ContentParts = contentParts
    });

    StreamingLoop(onUpdate, onError, onToolCall, DefaultMaxIterations);
}
```

#### 步骤 4：提取公共方法以避免重复代码（可选，非必须）

如需要，可以将添加 user message 的逻辑提取为私有方法：

```csharp
private void AddUserMessage(string userMessage, List<MessageContent> contentParts = null)
{
    _conversationHistory.Add(new ConversationMessage
    {
        Role = MessageRole.User,
        Content = userMessage,
        ContentParts = contentParts
    });
}
```

然后现有的 `Run(string)` 改为调用此方法。

---

## 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `MessageContent.cs` | 新增静态工厂方法：`CreateImageFromUrl()`, `CreateImageFromBase64()`, `CreateText()` |
| `AIAgent.cs` | 新增 `Run(string, List<MessageContent>, Action)` 重载；新增 `RunStreaming(string, List<MessageContent>, ...)` 重载；可选：提取 `AddUserMessage()` 私有方法 |

---

## 预期效果

**重构后：**

```csharp
var agent = AIAgent.CreateWithDefaults(client, "gpt-4o", "You are a helpful assistant");

// 文本对话（向后兼容）
var result = agent.Run("Hello!");

// 多模态对话（新功能）
var contentParts = new List<MessageContent>
{
    MessageContent.CreateImageFromUrl("https://example.com/photo.jpg", "auto")
};
var result = agent.Run("What is in this image?", contentParts);

// 多个图片 + 文本
var contentParts = new List<MessageContent>
{
    MessageContent.CreateText("Here are two images:"),
    MessageContent.CreateImageFromUrl("https://example.com/img1.jpg"),
    MessageContent.CreateImageFromBase64(base64Data, "image/jpeg")
};
var result = agent.Run("Compare these images", contentParts);

// 流式多模态
agent.RunStreaming("Describe this image", contentParts, 
    onUpdate: text => Console.Write(text),
    onError: err => Console.WriteLine(err.Message));
```

---

## 验证标准

1. ✅ 新 `Run(string, List<MessageContent>, ...)` 重载正常工作
2. ✅ 新 `RunStreaming(string, List<MessageContent>, ...)` 重载正常工作
3. ✅ 现有 `Run(string)` / `RunStreaming(string, ...)` 不受影响
4. ✅ `MessageContent` 工厂方法正确创建各类型内容块
5. ✅ C# 4.0 兼容（无 `?.`、`$""`、`nameof` 等）
6. ✅ 完整解决方案（SDK + Tests + Samples）编译 0 错误 0 警告
7. ✅ ContentParts 正确传递到 `OpenAIClient` / `AnthropicClient` 的消息转换逻辑