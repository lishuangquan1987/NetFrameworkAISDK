# 项目级编码规则

## 基础规则（继承自 Karpathy 全局编码规则）

### 1. Think Before Coding（编码前先思考）
- 明确陈述假设 —— 如果不确定，询问而非猜测
- 存在歧义时不要默默选择，呈现多种解释
- 必要时提出异议 —— 如果存在更简单的方法，要说出来
- 困惑时停止 —— 指出不清楚的地方并请求澄清

### 2. Simplicity First（简洁优先）
- 不添加未被要求的功能
- 不为单次使用的代码创建抽象
- 不实现未被请求的"灵活性"或"可配置性"
- 不为不可能的场景添加错误处理
- 如果能用 50 行完成，绝不写 200 行

### 3. Surgical Changes（外科式修改）
- 只修改必须修改的部分，只清理自己造成的混乱
- 不"改进"相邻的代码、注释或格式
- 不重构未损坏的代码
- 匹配现有风格，即使你会用不同方式处理

### 4. Goal-Driven Execution（目标驱动执行）
- 定义成功标准，循环直到验证通过
- 多步骤任务使用清晰的计划格式

---

## 项目特定规则

参考 Anthropic SDK：https://github.com/anthropics/anthropic-sdk-csharp

参考 OpenAi SDK:https://github.com/openai/openai-dotnet

参考 Microsoft Agent Framework:https://github.com/microsoft/agent-framework/tree/main/dotnet

### 项目基本信息
- **项目名称**：NetFrameworkAISDK
- **项目类型**：类库项目（Class Library）
- **主要命名空间**：
  - `NetFrameworkAISDK` - 核心命名空间
  - `NetFrameworkAISDK.OpenAI` - OpenAI 相关功能
  - `NetFrameworkAISDK.Anthropic` - Anthropic 相关功能

### 技术栈约束
- **.NET Framework 4.0+ 兼容性**：所有代码必须兼容 .NET Framework 4.0 及以上版本
  - 禁止使用 C# 6.0+ 特性（如 null 条件运算符 `?.`、字符串插值 `$""`、表达式体成员等）
  - 禁止使用 `IAsyncEnumerable` 和 `await foreach`（C# 8.0 特性）
  - 使用 .NET Framework 4.0 支持的 API

- **HTTP 请求**：使用 HttpWebRequest 进行 HTTP 请求（兼容 .NET Framework 4.0）

- **JSON 处理**：
  - 所有请求/响应 JSON 必须封装为强类型类
  - 禁止字符串拼接 JSON
  - 使用 Newtonsoft.Json 作为 JSON 序列化库

### 代码风格
- 遵循 C# 编码约定（PascalCase 用于类、方法、属性，camelCase 用于局部变量和参数）
- 使用 `var` 关键字当类型从右侧明显可见时
- 每个类/方法/属性应有适当的 XML 文档注释（用于公共 API）

### 项目结构
- 按功能模块组织代码（OpenAI、Anthropic、Common 等文件夹）
- 公共 API 应简洁易用，参考 Microsoft Agent Framework 的设计，但可以适当简化

### API 设计
- **API 密钥/配置管理**：通过构造函数传入，SDK 层不负责读取配置
- **流式回复**：使用 `Action<T>` 回调方式实现
- **工具调用**：简化实现，但保持简单易用

### 错误处理
- API 调用失败时返回错误对象，不抛出异常

### 测试
- 需要编写单元测试
- 使用 .NET Framework 4.0 最流行的测试框架

### 依赖管理
- 使用 NuGet 管理依赖
- 记录所有依赖及其版本在 `packages.config` 或项目文件中

---

## 验证标准

每次完成代码修改后，验证以下检查项：
1. 代码兼容 .NET Framework 4.0
2. HTTP 请求使用 HttpWebRequest
3. JSON 处理使用强类型类，无字符串拼接
4. 遵循现有代码风格
5. 没有添加未被要求的功能
6. 所有公共 API 有适当的文档注释
7. API 密钥通过构造函数传入
8. 流式回复使用 `Action<T>` 回调
9. 错误处理返回错误对象，不抛出异常
10. 命名空间使用 `NetFrameworkAISDK.*` 格式
