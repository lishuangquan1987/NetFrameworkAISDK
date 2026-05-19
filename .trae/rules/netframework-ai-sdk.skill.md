---
name: "netframework-ai-sdk"
description: ".NET Framework 4.0 + C# 5.0 大模型 API SDK 开发指南。包含 JSON snake_case 序列化、TLS 1.2 配置、Flurl URL 构建、OpenAI/Anthropic Agent 工具调用、AgentTools 文件/搜索工具、MAF 渐进式 SkillManager、MCP 客户端等模式。当在 .NET 4.0 项目中对接大模型 API 或构建 AI Agent 时使用。"
---

# .NET Framework 4.0 大模型 API SDK - 关键模式

综合踩坑总结，适用于 .NET 4.0 项目中对接 OpenAI / Anthropic 等大模型 API 的场景。

---

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

---

## 2. TLS 1.2 for .NET Framework 4.0

.NET 4.0 默认只启用 TLS 1.0，现代 API 服务要求 TLS 1.2。错误现象：`请求被中止: 未能创建 SSL/TLS 安全通道。`

```csharp
static HttpClientBase()
{
    ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
}
```

---

## 3. HTTP 请求：Flurl URL 构建 + HttpWebRequest

Flurl.Http 不支持 .NET 4.0（仅 net45+）。正确做法：使用 Flurl.dll 的 `Url` 类构建 URL，用 `HttpWebRequest` 发送请求。

```csharp
using Flurl;
protected readonly Url BaseUrl;
// 全程使用 Flurl Url 对象链式构建，最后一步 ToString()
var url = new Url(BaseUrl.ToString()).AppendPathSegment(endpoint).SetQueryParams(queryParams);
HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url.ToString());
```

---

## 4. AIFunction / AIAgent 工具调用

**AIFunction 手动构造（支持自定义参数 schema）**：
```csharp
new AIFunction
{
    Name = "tool_name",
    Description = "tool description",
    Parameters = new Dictionary<string, object>
    {
        { "type", "object" },
        { "properties", new Dictionary<string, object> { ... } },
        { "required", new List<string> { "paramName" } }
    },
    Execute = new Func<string, string>(argsJson => {
        var args = JsonHelper.Deserialize<Dictionary<string, string>>(argsJson);
        return result;
    })
};
```

**Agent 运行**：
```csharp
var agent = new AIAgent(client, model, instructions, tools);
var response = agent.Run(userMessage, onToolCall: (name, args, result) => {
    Console.WriteLine(">>> Tool: " + name);
});
```

---

## 5. AgentTools - 常用 Agent 工具

`AgentTools.CreateDefaultTools()` 返回 5 个常用工具，可直接注入 AIAgent：

| 工具 | 参数 | 说明 |
|------|------|------|
| `read_file(path)` | path | 读取文件 |
| `write_file(path, content)` | path, content | 写入文件 |
| `list_directory(path, pattern?)` | path, 可选 pattern | 列出目录 |
| `grep(pattern, path?, glob?)` | pattern, 可选路径/glob | 正则搜索文本 |
| `glob(pattern, path?)` | pattern, 可选路径 | 查找文件 |

用法：
```csharp
var tools = AgentTools.CreateDefaultTools();
var agent = new AIAgent(client, model, instructions, tools);
```

---

## 6. SkillManager - MAF 渐进式披露模式（重要）

Skill 的正文 **绝不能全部加载到 system prompt 中**。正确做法是 MAF 渐进式披露：

**流程**：
1. `DiscoverSkills(path)` → 找到 SKILL.md 文件
2. `BuildProgressivePrompt(skills)` → 只生成 XML 目录（name + description）
3. `CreateLoadSkillFunction(skills)` → 创建 `load_skill` 函数工具
4. LLM 按需调用 `load_skill("skill-name")` → 获取完整正文

```csharp
// ✅ 正确：渐进式披露
var skills = SkillManager.DiscoverSkills("./skills");
var prompt = SkillManager.BuildProgressivePrompt(skills);
var loadSkill = SkillManager.CreateLoadSkillFunction(skills);
var agent = new AIAgent(client, model, prompt, new[] { loadSkill });
```

**XML 目录格式**：
```
# Available Skills
When a task aligns with a skill's domain, call load_skill to retrieve instructions.
<available_skills>
  <skill><name>pdf-processing</name><description>Extract PDF text</description></skill>
</available_skills>
```

**禁止做法**（违反 MAF 规范）：
- ❌ 手动读取 SKILL.md 正文塞到 system prompt
- ❌ 一次性加载所有 skill 内容（was: `LoadCombinedInstructions()`）
- ❌ 关键词匹配预过滤（was: `SelectRelevantSkills()`）

---

## 7. MCP 客户端

```csharp
using var mcp = new McpClient();
mcp.Connect("path/to/server", "args");
mcp.Initialize();
var tools = mcp.ListTools();
var result = mcp.CallTool("name", args);
mcp.Shutdown();
```

MCP 工具转 AIFunction：
```csharp
var func = AIFunctionFactory.CreateFromMcpTool(name, desc, schema, execute);
```

---

## 8. C# 4.0 兼容性检查清单

| 禁止写法 | 正确写法 |
|---------|---------|
| `obj?.Prop` | `if (obj != null) { ... }` |
| `a ?? b` | `a != null ? a : b` |
| `$"x={x}"` | `string.Format("x={0}", x)` |
| `nameof(Prop)` | `"Prop"` 字符串字面量 |
| `=> expr` 表达式体 | 完整 `{ return ...; }` |
| 方法组转 Delegate | `new Func<...>(MethodName)` |

```powershell
# 验证方法
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' '<project>.csproj' /t:Rebuild /p:Configuration=Debug 2>&1
# 期望: 0 错误, 0 警告
```