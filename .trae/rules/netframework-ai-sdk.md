# NetFrameworkAI SDK - 关键模式总结

## 1. JSON 序列化：必须使用 snake_case

**场景**：对接 OpenAI / Anthropic 等大模型 API

**问题**：.NET 属性默认用 PascalCase，OpenAI API 使用 snake_case（`tool_calls`、`finish_reason`、`max_tokens`），用 `CamelCasePropertyNamesContractResolver` 会导致字段不匹配

**正确做法**：
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

**影响**：
- `ToolCalls` → `tool_calls` ✅（而非 `toolCalls`）
- `FinishReason` → `finish_reason` ✅（而非 `finishReason`）
- `MaxTokens` → `max_tokens` ✅（而非 `maxTokens`）
- `ToolCallId` → `tool_call_id` ✅（而非 `toolCallId`）

---

## 2. TLS 1.2 for .NET Framework 4.0

**场景**：.NET 4.0 默认只启用 TLS 1.0，现代 API 服务要求 TLS 1.2

**正确做法**：
```csharp
static HttpClientBase()
{
    ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
}
```
3072 = TLS 1.2，768 = TLS 1.1，192 = TLS 1.0

---

## 3. HTTP 请求：Flurl URL 构建 + HttpWebRequest

**场景**：Flurl.Http 不支持 .NET 4.0（仅 net45+）

**正确做法**：使用 Flurl.dll 的 `Url` 类构建 URL，用 `HttpWebRequest` 发送请求

```csharp
using Flurl;

// 存储为 Url 对象
protected readonly Url BaseUrl;

// 构建 URL
private string BuildUrl(string endpoint)
{
    return new Url(BaseUrl.ToString()).AppendPathSegment(endpoint).ToString();
}

// 构建带查询参数的 URL
private string BuildUrl(string endpoint, object queryParams)
{
    return new Url(BaseUrl.ToString())
        .AppendPathSegment(endpoint)
        .SetQueryParams(queryParams)
        .ToString();
}
```

---

## 4. 工具调用 Agent 流程

**场景**：模型回复中包含 `tool_calls` 时，需执行工具并将结果返回给模型

**正确判断方式**：检查 `assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0`，**不依赖** `FinishReason == "tool_calls"`

```csharp
bool hasToolCalls = assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0;

if (hasToolCalls)
{
    ExecuteToolCalls(assistantMessage, onToolCall);  // 执行工具
    // 工具结果已添加到 conversationHistory 中
    // 自动进入下一轮循环
}
else
{
    return result;  // 返回模型的文字回复
}
```

**消息历史结构**：
```
User: "现在几点"
Assistant: { content: "我来查一下", tool_calls: [...] }   ← 带工具调用的回复
Tool: { tool_call_id: "call_xxx", content: "当前时间 13:04" }  ← 工具执行结果
Assistant: { content: "当前时间是13:04" }                    ← 模型最终回复
```

---

## 5. C# 4.0 兼容性检查清单

- 不使用 `?.` null 条件运算符 → 改用 `if (x != null)`
- 不使用 `??` null 合并运算符 → 改用 `?:` 或 `if` 判断
- 不使用 `$""` 字符串插值 → 改用 `string.Format()` 或 `+` 拼接
- 不使用 `nameof` → 直接写字符串
- 不使用表达式体成员 `=>` → 写完整 `get { return ...; }`
- 方法组不能隐式转 `Delegate` → 用 `new Func<...>(MethodName)` 包装
- 命名参数必须在所有位置参数之后