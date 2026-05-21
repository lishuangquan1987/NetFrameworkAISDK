# 重构计划：多 Skills 目录、工具安全确认、会话压缩

---

## 一、多 Skills 目录支持

### 开源调研

**Claude Code 三层架构**（参考 [Claude Code Skills 文档](https://docs.claude.com/en/docs/claude-code/skills)）：

| 层级 | 路径 | 作用域 | 用途 |
|------|------|--------|------|
| User (全局) | `~/.claude/skills/` | 所有项目 | 通用技能，跨项目共享 |
| Project (项目) | `.claude/skills/` | 当前项目 | 项目特定技能，可通过 Git 共享 |
| Local (本地) | `.claude/skills.local/` | 当前机器 | 本机个性化覆盖 |

**优先级规则**：项目覆盖全局，本地覆盖项目。同名 Skills 按优先级决定生效版本。

**Microsoft Agent Framework / OpenAI Agents SDK**：没有独立的 Skill 管理器概念。Skills 本质是带专用 instructions 的子 Agent 或工具，通过 `tools:` 参数统一注入。

### 当前项目状态

```csharp
// 当前只支持单个目录
public AIAgent(..., string skillsDirectory)
// SkillManager.DiscoverSkills(string directoryPath) — 只接受一个路径
```

### 改造方案

#### 1.1 AIAgent 构造函数支持多目录

```csharp
// 新构造函数（单目录仍兼容，通过 params 或 string[] 扩展）
public AIAgent(IAIClient client, string model, string instructions,
    IEnumerable<AIFunction> tools, bool includeDefaultTools,
    string[] skillsDirectories)  // string → string[]
```

#### 1.2 SkillManager.DiscoverSkills 支持多目录 + 去重

```csharp
// 新增多目录扫描方法
public static List<SkillInfo> DiscoverSkills(string[] directoryPaths)
{
    var allSkills = new List<SkillInfo>();
    var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 反向遍历（后面的优先级更高：global → project → local）
    for (int i = directoryPaths.Length - 1; i >= 0; i--)
    {
        var dirSkills = DiscoverSkills(directoryPaths[i]); // 复用单目录扫描
        foreach (var skill in dirSkills)
        {
            if (!seenNames.Contains(skill.Name))
            {
                seenNames.Add(skill.Name);
                allSkills.Insert(0, skill); // 高优先级排前面
            }
        }
    }
    return allSkills;
}
```

优先级：`directoryPaths` 数组中越靠后的目录优先级越高。`["~/.agents/", "./skills", "./skills.local"]` → local > project > global。

#### 1.3 静态工厂方法更新

```csharp
// CreateWithDefaults 支持多目录
public static AIAgent CreateWithDefaults(
    IAIClient client, string model, string instructions,
    string[] skillsDirectories = null,          // string → string[]
    IEnumerable<AIFunction> extraTools = null)

// 辅助：快捷创建三层目录
public static string[] GetDefaultSkillPaths(string projectSkillsDir = null)
{
    var paths = new List<string>();
    paths.Add(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".agents"));                             // ~/.agents/
    if (!string.IsNullOrEmpty(projectSkillsDir))
        paths.Add(projectSkillsDir);            // ./skills/
    paths.Add(Path.Combine(
        projectSkillsDir ?? ".", "skills.local")); // ./skills.local/
    return paths.ToArray();
}
```

### 预期效果

```csharp
// 之前：单个目录
var agent = AIAgent.CreateWithDefaults(client, "gpt-4o", "...", "./skills");

// 之后：三层目录
var agent = AIAgent.CreateWithDefaults(
    client, "gpt-4o", "...",
    new[] { "~/.agents", "./skills", "./skills.local" });

// 便捷方式：自动三层
var agent = AIAgent.CreateWithDefaults(client, "gpt-4o", "...",
    AIAgent.GetDefaultSkillPaths("./skills"));
```

### 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `AIAgent.cs` | `skillsDirectory` → `skillsDirectories`（string[]）；构造函数、工厂方法、`IntegrateSkills` 内部逻辑调整 |
| `SkillManager.cs` | 新增 `DiscoverSkills(string[])` 多目录扫描（去重 + 优先级） |

---

## 二、工具调用安全确认（Human-in-the-Loop）

### 开源调研

**OpenAI Agents SDK**（Python，[源码](https://github.com/openai/openai-agents-python/blob/main/docs/human_in_the_loop.md)）：

```python
# 方式 1：静态标记
@function_tool(needs_approval=True)
async def cancel_order(order_id: int) -> str: ...

# 方式 2：动态判断（根据参数决定）
@function_tool(needs_approval=lambda ctx, params, id: "refund" in params.get("subject", ""))
async def send_email(subject: str, body: str) -> str: ...
```

**审批流程**：
1. 模型发出 tool call → Runner 评估 `needs_approval`
2. 需要审批 → Runner 暂停，返回 `RunResult.interruptions` 含 `ToolApprovalItem`
3. 用户决定 → `state.approve(id)` 或 `state.reject(id)`
4. 恢复运行 → `Runner.run(agent, state)` 从断点继续

**PydanticAI**：`@agent.tool(requires_approval=True)` 或抛 `ApprovalRequired` 异常。Agent run 结束并返回 `DeferredToolRequests`，审批后构建 `DeferredToolResults` 继续执行。

### 当前项目状态

```csharp
// AIFunction 无安全标记
public class AIFunction
{
    public string Name { get; set; }
    public string Description { get; set; }
    public object Parameters { get; set; }
    public Func<string, string> Execute { get; set; }
    // 缺少: RequiresApproval 属性
}

// ExecuteToolCalls 直接执行，无拦截
private void ExecuteToolCalls(List<ToolCallRequest> toolCalls, Action<ToolCallEventArgs> onToolCall)
{
    foreach (var toolCall in toolCalls)
    {
        // 直接执行，没有任何审批机制
        var result = function.Execute(functionArgs);
        ...
    }
}
```

### 改造方案

#### 2.1 AIFunction 添加安全标记

```csharp
public class AIFunction
{
    // 现有属性保持不变...

    /// <summary>
    /// 是否需要用户确认后才能执行。为 true 时 Agent 会暂停等待审批。
    /// 也可以是 Func 委托，根据参数动态判断。
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// 动态审批判断函数（可选，优先级高于 RequiresApproval）。
    /// 参数：(functionName, functionArguments) → 是否需要审批
    /// </summary>
    public Func<string, string, bool> ApprovalPredicate { get; set; }
}
```

#### 2.2 ToolCallEventArgs 添加审批字段

```csharp
public class ToolCallEventArgs : EventArgs
{
    // 现有属性保持不变...
    public string FunctionName { get; set; }
    public string FunctionArguments { get; set; }
    public string Result { get; set; }
    public string ToolCallId { get; set; }

    /// <summary>是否需要用户审批</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>审批状态：null=未决定, true=已批准, false=已拒绝</summary>
    public bool? IsApproved { get; set; }
}
```

#### 2.3 AIAgent 审批回调机制

```csharp
// AIAgent 新增：工具审批回调
// 返回 true = 批准执行，false = 拒绝（跳过该工具）
public delegate bool ToolApprovalCallback(ToolCallEventArgs args);

// 新增 Run 重载，支持审批回调
public ApiResponse<string> Run(
    string userMessage,
    Action<ToolCallEventArgs> onToolCall,
    ToolApprovalCallback onToolApproval)  // 新增参数

// 内部执行逻辑
private void ExecuteToolCalls(
    List<ToolCallRequest> toolCalls,
    Action<ToolCallEventArgs> onToolCall,
    ToolApprovalCallback onToolApproval)
{
    foreach (var toolCall in toolCalls)
    {
        AIFunction function = _functionMap.ContainsKey(functionName)
            ? _functionMap[functionName] : null;
        if (function == null) continue;

        bool needsApproval = function.RequiresApproval;
        if (function.ApprovalPredicate != null)
            needsApproval = function.ApprovalPredicate(functionName, functionArgs);

        if (needsApproval && onToolApproval != null)
        {
            var args = new ToolCallEventArgs { ... };
            if (!onToolApproval(args))
            {
                // 被拒绝，添加拒绝消息到历史
                _conversationHistory.Add(new ConversationMessage
                {
                    Role = MessageRole.Tool,
                    Name = functionName,
                    ToolCallId = toolCall.Id,
                    Content = "[REJECTED] User denied execution of tool: " + functionName
                });
                continue;
            }
        }

        // 正常执行...
        var result = function.Execute(functionArgs);
        ...
    }
}
```

#### 2.4 AIFunctionFactory 支持 RequiresApproval

```csharp
// 创建带安全标记的函数
public static AIFunction CreateWithApproval(
    Func<string, string> execute,
    string name,
    string description,
    object parameters,
    bool requiresApproval = false,
    Func<string, string, bool> approvalPredicate = null)
```

### 预期效果

```csharp
// 标记敏感工具
var deleteFile = new AIFunction
{
    Name = "delete_file",
    Description = "Delete a file from disk",
    Execute = args => { File.Delete(...); return "deleted"; },
    RequiresApproval = true
};

// 运行时带审批回调
var result = agent.Run("Delete temp files", onToolCall: null, onToolApproval: args =>
{
    Console.WriteLine("Approve: {0} with args {1}? (y/n)", args.FunctionName, args.FunctionArguments);
    return Console.ReadLine() == "y";
});
```

### 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `AIFunction.cs` | +`RequiresApproval` 属性、+`ApprovalPredicate` 属性 |
| `ToolCallEventArgs.cs` | +`RequiresApproval` 字段、+`IsApproved` 字段 |
| `AIAgent.cs` | `ExecuteToolCalls` 增加审批流程；新增 `ToolApprovalCallback` 委托；`Run`/`RunStreaming` 增加审批回调参数 |
| `AIFunctionFactory.cs` | 新增 `CreateWithApproval` 方法 |

---

## 三、实施顺序

```
Phase 1: 基础能力（本次执行）
  1.1 多 Skills 目录（AIAgent + SkillManager）
  1.2 工具安全确认（AIFunction + AIAgent）

Phase 2: 测试 + 示例（本次执行）
  2.1 单元测试（SkillManager 多目录、工具审批）
  2.2 Samples 示例程序
```

---

## 四、验证标准

1. ✅ 多 Skills 目录正确去重，优先级 local > project > global
2. ✅ `RequiresApproval=true` 的工具通过审批回调拦截
3. ✅ `ApprovalPredicate` 动态判断生效
4. ✅ C# 4.0 兼容（无 `?.`、`$""`、`nameof`、`=>` 表达式体）
5. ✅ 完整解决方案编译 0 错误 0 警告
6. ✅ 现有 API 向后兼容