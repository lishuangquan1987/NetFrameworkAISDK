# 重构计划：将 Skill 和 Tools 集成到 AIAgent 中

## 调研总结：开源项目如何处理

### 1. Microsoft Agent Framework (MAF)
```
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "...",
    tools: [AIFunctionFactory.Create(GetWeather)]
);
```
- **核心模式**：一切能力（工具函数、技能知识）在 Agent 构造时统一传入 `tools:` 参数
- 没有单独的 "SkillManager" — Skills 就是带特定 instructions 的 sub-agent 或工具
- `AIFunctionFactory.Create()` 统一创建一切工具
- "渐进式披露"通过 Agent 自身的 instructions + tools 组合实现

### 2. OpenAI Agents SDK (Python)
```python
agent = Agent(
    name="Support",
    instructions="...",
    tools=[WebSearchTool(), function_tool(get_weather)]
)
```
- `tools` 参数直接接受一切：hosted tools、function tools、agents as tools
- Skill/知识 = Agent 的 `instructions` + 专用 sub-agent
- 不存在独立的 Skill 管理器，Agent 是一切能力的容器

### 3. Claude Agent SDK (Python)
```python
@tool("query_db", "...", {...})
async def query_database(args): ...

server = create_sdk_mcp_server(name="tools", tools=[query_database])
options = ClaudeAgentOptions(mcp_servers={"tools": server})
```
- 工具通过 MCP Server 统一注册
- Custom tools 和 built-in tools 统一通过 `allowed_tools` 管理
- Skills 通过 `.claude/skills/` 目录 + system prompt 注入实现（类似当前 SkillManager 的渐进式披露模式）

### 关键结论
**三个主流框架的共同模式：**
1. Agent 是**唯一入口**，构造时接收一切能力（tools、instructions、skills）
2. 不存在独立的 SkillManager 或 ToolManager 类
3. Skill 的本质 = 一组专用 instructions + 工具函数，可以作为 AIAgent 的子实例
4. "渐进式披露"通过 `load_skill` 工具实现，但这个工具也是 Agent tools 的一部分

---

## 当前问题

当前 SDK 中 Skill 和 Tools 使用分散：

```csharp
// 当前：需要三层代码才能让 Agent 具备 Skills
var skills = SkillManager.DiscoverSkills("./skills");                    // 1. 发现技能
var prompt = SkillManager.BuildProgressivePrompt(skills);               // 2. 构建提示词
var loadSkillFn = SkillManager.CreateLoadSkillFunction(skills);          // 3. 创建加载工具
var tools = AgentTools.CreateDefaultTools();                            // 4. 创建默认工具
tools.Add(loadSkillFn);                                                 // 5. 手动合并
var agent = new AIAgent(client, model, prompt + instructions, tools);   // 6. 构造 Agent
```

**问题**：
1. `SkillManager` 和 `AgentTools` 是静态工具类，需要调用方手动编排
2. `AIAgent` 不知道 Skills 存在，无法自动管理技能的渐进式披露
3. Tool 注册需要通过 AgentTools + SkillManager 两套 API，各自独立
4. 使用方代码冗长，违反 MAF/OpenAI 的 "一行构造" 理念

---

## 重构方案

### 设计原则（参考 MAF 和 OpenAI Agents SDK）

1. **AIAgent 是一切能力的唯一入口**
2. **构造时传入一切**（instructions + tools + skills 目录），内部自动编排
3. **Skills 自动集成**：传入 skills 目录 → Agent 自动 Discover + BuildProgressivePrompt + CreateLoadSkillFunction
4. **Tools 统一注册**：`AgentTools.CreateDefaultTools()` 变为 Agent 的可选内置能力
5. **向后兼容**：保持现有 API 可用，新增便捷方法

### 具体步骤

#### 步骤 1：扩展 AIAgent 构造函数

新增重载，支持直接传入 skills 目录路径：

```csharp
// 新 API：一行构造
var agent = new AIAgent(
    client,
    model: "gpt-4o",
    instructions: "You are a helpful coding assistant",
    tools: new List<AIFunction>(),           // 用户自定义工具
    includeDefaultTools: true,               // 自动包含 AgentTools.CreateDefaultTools()
    skillsDirectory: "./skills"              // 自动 Discover + 注入渐进式提示词 + 注册 load_skill/read_skill 工具
);
```

**内部自动做的事：**
1. 如果 `includeDefaultTools=true`，自动调用 `AgentTools.CreateDefaultTools()` 并合并
2. 如果 `skillsDirectory` 不为 null/empty，自动调用：
   - `SkillManager.DiscoverSkills(skillsDirectory)`
   - `SkillManager.BuildProgressivePrompt(skills)` → 追加到 system prompt
   - `SkillManager.CreateLoadSkillFunction(skills)` → 添加到工具列表
   - `SkillManager.CreateReadSkillTool(skills)` → 添加到工具列表

#### 步骤 2：AIAgent 内部管理 Skills 状态

新增私有字段和方法：

```csharp
private readonly List<SkillInfo> _skills;        // 已发现的技能列表
private readonly string _skillsDirectory;         // 技能目录路径
private bool _skillsIntegrated;                   // 是否已集成技能

// 自动集成 Skills 的方法
private void IntegrateSkills(string skillsDirectory, ...)
{
    _skills = SkillManager.DiscoverSkills(skillsDirectory);
    // 追加渐进式提示词到 system prompt
    // 注册 load_skill / read_skill 工具
}
```

#### 步骤 3：新增便捷构造方法（Builder 模式简化版）

```csharp
// 便捷创建方法 1：带默认工具 + Skills
public static AIAgent CreateWithDefaults(
    IAIClient client,
    string model,
    string instructions,
    string skillsDirectory = null,
    IEnumerable<AIFunction> extraTools = null)

// 便捷创建方法 2：不带默认工具
public static AIAgent CreateMinimal(
    IAIClient client,
    string model,
    string instructions,
    IEnumerable<AIFunction> tools = null)
```

#### 步骤 4：保持 SkillManager 和 AgentTools 向后兼容

- `SkillManager` 保留为 `public static` 类，高级用户可手动调用
- `AgentTools.CreateDefaultTools()` 保持不变
- 已有 `AIAgent(string, string, IEnumerable<AIFunction>)` 构造函数签名不修改
- 仅新增重载和便捷方法

---

## 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `AIAgent.cs` | 新增构造函数重载（支持 `includeDefaultTools` + `skillsDirectory`）；新增 `CreateWithDefaults` / `CreateMinimal` 静态工厂方法；内部管理 Skills 状态 |
| `SkillManager.cs` | 无需修改（保留向后兼容） |
| `AgentTools.cs` | 无需修改（保留向后兼容） |

---

## 预期效果

**重构前（5+ 行）：**
```csharp
var skills = SkillManager.DiscoverSkills("./skills");
var skillPrompt = SkillManager.BuildProgressivePrompt(skills);
var loadSkillFn = SkillManager.CreateLoadSkillFunction(skills);
var readSkillFn = SkillManager.CreateReadSkillTool(skills);
var defaultTools = AgentTools.CreateDefaultTools();
defaultTools.Add(loadSkillFn);
defaultTools.Add(readSkillFn);
var agent = new AIAgent(client, model, instructions + "\n" + skillPrompt, defaultTools);
```

**重构后（1 行）：**
```csharp
var agent = AIAgent.CreateWithDefaults(client, "gpt-4o", "You are a helpful assistant", "./skills");
```

---

## 验证标准

1. ✅ 新构造函数能正确集成默认工具 + Skills
2. ✅ 现有构造函数/tests 不破坏
3. ✅ `SkillManager` 和 `AgentTools` 仍可独立使用
4. ✅ C# 4.0 兼容（无 `?.`、`$""`、`nameof` 等）
5. ✅ skills 目录不存在时不报错，静默跳过
6. ✅ `includeDefaultTools=false` 时不添加默认工具