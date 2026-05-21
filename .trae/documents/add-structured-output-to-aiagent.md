# 重构计划：AIAgent 支持结构化 JSON 输出（Instructor 模式）

## 调研总结：开源项目如何处理结构化输出

### 1. Instructor 库 — 最流行模式
```csharp
// Instructor for .NET — 用户定义类型，自动生成 Schema，直接获得强类型结果
var result = await client.Chat.Completions.CreateAsync(new ChatCompletionRequest
{
    Messages = messages,
    ResponseModel = typeof(UserInfo)  // ← 只需传类型
});
var user = result.Deserialize<UserInfo>();  // ← 直接得到强类型对象
```
- **核心哲学**：用户只需定义 .NET class，Schema 生成、Provider 适配、反序列化全自动
- OpenAI 后端 → 用原生 `response_format`（性能最好）
- Anthropic 后端 → 用 tool-use hack（自动透明）
- **开发体验**：定义 C# 类 → 得到强类型对象，零手工拼接

### 2. OpenAI Agents SDK (Python)
```python
@dataclass
class WeatherInfo:
    city: str
    temperature: float

result = await Runner.run(agent, "...", output_type=WeatherInfo)
print(result.final_output.city)  # ← 直接访问属性
```
- `output_type` 参数自动处理 Schema 生成 + 结果解析

### 3. Microsoft Agent Framework (MAF)
```csharp
ChatResponse<WeatherInfo> response = await agent.GetResponseAsync<WeatherInfo>(userMessage);
```
- Agent 级别泛型结构化输出，一行搞定

### 关键结论

| 框架 | 用户体验 | 内部实现 |
|------|----------|----------|
| Instructor | `Deserialize<T>()` | OpenAI: response_format / 其他: tool-use hack |
| OpenAI Agents SDK | `output_type=WeatherInfo` | 原生 response_format |
| MAF | `GetResponseAsync<T>()` | response_format + fallback |
| **本项目目标** | `RunStructured<WeatherInfo>("...")` | OpenAI: response_format / Anthropic: tool-use hack |

**目标模式：传类型，拿结果。Schema 生成、Provider 适配、反序列化全部自动化。**

---

## 当前状态分析

无结构化输出支持。
- `ConversationOptions` / `ChatCompletionRequest` 均无 `response_format`
- `AIAgent.Run()` 只返回自由文本

---

## 重构方案（Instructor 模式）

### 设计哲学

1. **传类型，拿结果**：`RunStructured<WeatherInfo>("...")` → `ApiResponse<WeatherInfo>`
2. **Schema 自动生成**：反射分析 .NET 类型属性，自动构建 JSON Schema
3. **Provider-Aware 策略**：OpenAI → 原生 response_format；Anthropic → tool-use hack
4. **零侵入 AgentLoop**：在请求/响应层处理，AgentLoop 完全不变

### 具体步骤

#### 步骤 1：新增 `Common/JsonSchemaGenerator.cs` — Schema 自动生成

```csharp
public static class JsonSchemaGenerator
{
    /// <summary>从 .NET 类型生成 JSON Schema 字符串</summary>
    public static string GenerateFromType(Type type, string schemaName)
    {
        // 反射遍历 type 的 public 属性
        // 映射: string→"string", int/long/double/float→"number", bool→"boolean"
        //        DateTime→"string", List<T>/T[]→"array", 嵌套 class→"object"
        // 返回 JSON Schema 字符串
    }
}
```

#### 步骤 2：新增 `Common/ResponseFormat.cs` — 通用 ResponseFormat 数据类

```csharp
public class ResponseFormat
{
    public string Type { get; set; }        // "json_schema"
    public string JsonSchema { get; set; }  // JSON Schema 字符串
    public string SchemaName { get; set; }  // 用于 OpenAI strict mode
    public bool Strict { get; set; }        // OpenAI strict mode
}
```

#### 步骤 3：修改 `Common/ConversationOptions.cs` — 新增属性

```csharp
public ResponseFormat ResponseFormat { get; set; }
```

#### 步骤 4：OpenAI 层 — 新增请求类 + 修改 ChatCompletionRequest + 透传

新增 `OpenAI/OpenAiResponseFormat.cs` 和 `OpenAI/JsonSchemaObject.cs`：
```csharp
public class OpenAiResponseFormat
{
    public string Type { get; set; }
    public JsonSchemaObject JsonSchema { get; set; }
}
public class JsonSchemaObject
{
    public string Name { get; set; }
    public bool Strict { get; set; }
    public object Schema { get; set; }
}
```

ChatCompletionRequest 新增：
```csharp
public OpenAiResponseFormat ResponseFormat { get; set; }
```

OpenAIClient.SendConversation：将 `options.ResponseFormat` → 映射到 ChatCompletionRequest 的 `ResponseFormat`

#### 步骤 5：Anthropic 层 — tool-use hack 注入 + 响应拦截

**注入端**（AnthropicClient.SendConversation）：
- 检测 `options.ResponseFormat`
- 自动向 tools 列表注入一个名为 `structured_output` 的隐藏工具
- 该工具的 `input_schema` = 用户的 JSON Schema

**拦截端**（AnthropicClient.ConvertFromAnthropicResponse）：
- 检测响应中是否有 `name: "structured_output"` 的 tool_use 块
- 如果有：提取 `input` 字段 → 序列化为 JSON → 设为 `ConversationResponse.Content`
- **不**将其添加到 ToolCalls 列表（避免 Agent 误执行）
- 模型看到只有文本响应，AgentLoop 自然退出

#### 步骤 6：AIAgent — 新增 `RunStructured<T>()` 方法

```csharp
/// <summary>
/// 执行结构化对话，AI 输出强类型对象
/// </summary>
/// <typeparam name="T">期望的输出类型</typeparam>
/// <param name="userMessage">用户消息</param>
/// <param name="onToolCall">工具调用回调（可选）</param>
/// <returns>包含反序列化对象或错误信息的响应</returns>
public ApiResponse<T> RunStructured<T>(string userMessage, Action<ToolCallEventArgs> onToolCall = null)
{
    var schemaName = typeof(T).Name;
    var jsonSchema = JsonSchemaGenerator.GenerateFromType(typeof(T), schemaName);

    // 设置结构化输出选项
    _options.ResponseFormat = new ResponseFormat
    {
        Type = "json_schema",
        JsonSchema = jsonSchema,
        SchemaName = schemaName,
        Strict = true
    };

    var response = Run(userMessage, onToolCall);

    // 恢复状态
    _options.ResponseFormat = null;

    if (!response.IsSuccess)
    {
        return new ApiResponse<T> { Error = response.Error };
    }

    try
    {
        var result = JsonHelper.Deserialize<T>(response.Result);
        return new ApiResponse<T> { Result = result };
    }
    catch (Exception ex)
    {
        return new ApiResponse<T>
        {
            Error = new ApiError { Message = "Structured output parse failed: " + ex.Message }
        };
    }
}
```

---

## 需要修改/新增的文件

| 文件 | 操作 | 内容 |
|------|------|------|
| `Common/JsonSchemaGenerator.cs` | **新增** | 反射生成 JSON Schema |
| `Common/ResponseFormat.cs` | **新增** | 通用 ResponseFormat 数据类 |
| `Common/ConversationOptions.cs` | 修改 | +1 属性 `ResponseFormat` |
| `OpenAI/OpenAiResponseFormat.cs` | **新增** | OpenAI 专用序列化类 |
| `OpenAI/JsonSchemaObject.cs` | **新增** | OpenAI JSON Schema 对象 |
| `OpenAI/ChatCompletionRequest.cs` | 修改 | +1 属性 `ResponseFormat` |
| `OpenAI/OpenAIClient.cs` | 修改 | 透传 response_format |
| `Anthropic/AnthropicClient.cs` | 修改 | 注入 + 拦截 structured_output 工具 |
| `Common/AIAgent.cs` | 修改 | +1 方法 `RunStructured<T>()` |

---

## 预期效果

```csharp
// 1. 定义输出类型
public class WeatherInfo
{
    public string City { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }
}

// 2. 创建 Agent
var agent = AIAgent.CreateMinimal(client, "gpt-4o", "You are a weather assistant");

// 3. 一行调用，直接拿结果！
var result = agent.RunStructured<WeatherInfo>("What is the weather in Beijing?");

if (result.IsSuccess)
{
    Console.WriteLine("City: " + result.Result.City);         // "Beijing"
    Console.WriteLine("Temp: " + result.Result.Temperature);  // 22.5
    Console.WriteLine("Cond: " + result.Result.Condition);    // "Sunny"
}

// 4. 支持嵌套类型
public class Person { public string Name { get; set; } public int Age { get; set; } }
public class Team { public string Name { get; set; } public List<Person> Members { get; set; } }

var team = agent.RunStructured<Team>("List the Avengers team");
// team.Result.Members[0].Name == "Tony Stark"
```

---

## 验证标准

1. ✅ `JsonSchemaGenerator` 正确从 .NET 类型生成 JSON Schema
2. ✅ OpenAI: `response_format` 字段正确出现在 HTTP 请求体中
3. ✅ Anthropic: `structured_output` 工具自动注入并被正确拦截
4. ✅ Anthropic: 拦截后的 ToolCalls 列表不包含 `structured_output`
5. ✅ `RunStructured<T>()` 返回强类型 `ApiResponse<T>`，`Result` 已正确反序列化
6. ✅ 现有 `Run()` / `RunStreaming()` 零影响
7. ✅ C# 4.0 兼容
8. ✅ 完整解决方案 0 错误 0 警告