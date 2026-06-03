# NetFramework-AI-SDK 代码分析报告

## 项目概述
- **项目名称**: NetFrameworkAISDK
- **目标框架**: .NET Framework 4.0+ / .NET Standard 2.0
- **语言版本**: C# 5.0
- **主要依赖**: Newtonsoft.Json 13.0.1

---

## 一、Bug 清单

### 1. OpenAIClient 中 DeepSeek 特定逻辑硬编码 [高]
**文件**: src/NetFrameworkAISDK/OpenAI/OpenAIClient.cs

ConvertToOpenAiMessages 方法中硬编码了 DeepSeek 特定逻辑：
- chatMsg.Name = "assistant"（DeepSeek 要求 tool_calls 消息必须有 name）
- chatMsg.ReasoningContent = msg.ReasoningContent（DeepSeek 思考模式）

这会导致其他 OpenAI 兼容 API 出现意外行为。

**建议**: 创建 OpenAIClientOptions 配置类，将这些行为设为可选项，默认关闭。

回复：创建 OpenAIClientOptions 配置类，将这些行为设为可选项，默认开启。

---

### 2. AgentTools.RunCommand 命令注入防护不完整 [高]
**文件**: src/NetFrameworkAISDK/Common/AgentTools.cs | RunCommand 方法

当前仅阻止 & | ; \0 字符，缺少对以下内容的防护：
- 命令替换  、反引号
- 重定向 > < 
- 换行符注入
- 嵌套 shell 调用

**建议**: 采用白名单方式，只允许明确列出的安全命令。

回复：需要修复

---

### 3. SSE 流式响应解析不符合规范 [高]
**文件**: src/NetFrameworkAISDK/Common/HttpClientBase.cs | PostStream 方法

当前按行读取，假设每行"data:"后是完整 JSON。SSE 规范要求：
- 多行 data: 属于同一事件
- 事件以 \\n\\n 分隔
- 需缓冲不完整的行

**建议**: 实现符合 SSE 规范的解析器，按事件边界解析。

回复：需要修复

---

### 4. TLS 配置静态构造函数问题 [中]
**文件**: src/NetFrameworkAISDK/Common/HttpClientBase.cs

静态构造函数中配置 ServicePointManager.SecurityProtocol，在某些 .NET 4.0 环境 TLS 1.2 不可用时会导致异常。异常被 catch 但不影响功能——请求可能因协议不匹配失败。

**建议**: 运行时检测 TLS 1.2 支持性，或降级到 TLS 1.1/1.0。

回复：无需处理

---

### 5. AIAgent.MergeToolCall 空引用风险 [中]
**文件**: src/NetFrameworkAISDK/Common/AIAgent.cs

MergeToolCall 中 existing.FunctionArguments 可能为 null，虽然有三元运算符兜底，但更规范的做法是属性初始化时设为空字符串。

回复：需要处理

---

### 6. AnthropicClient 结构化输出工具名冲突 [中]
**文件**: src/NetFrameworkAISDK/Anthropic/AnthropicClient.cs

BuildStructuredOutputTool 创建的内部工具名为 "structured_output"，可能与用户工具重名。

回复：需要处理

---

### 7. AIAgent 对话历史修剪可能丢失系统消息 [中]
**文件**: src/NetFrameworkAISDK/Common/AIAgent.cs

PruneHistory 在修剪历史消息时，没有确保始终保留系统消息和最近的 N 条消息的顺序保护。

回复：需要处理

---

## 二、架构问题

### 1. AIClientBase 职责不单一
同时负责 HTTP 通信和工具管理（_tools、_toolMap）。应将工具管理移到 AIAgent 或独立 ToolManager。

回复：需要处理

### 2. 缺少 CancellationToken 支持
所有长时间操作（SendConversation、SendConversationStreaming、AgentLoop）均不支持取消。UI 场景下会导致线程卡死。

回复：需要处理

### 3. 线程安全不完整
_conversationHistory 的访问保护不一致，部分方法有锁（AddToHistory），部分没有（GetHistory、ClearHistory）。

回复：需要处理

### 4. 错误处理三种模式混用
- ApiResponse<T>（网络层）
- 异常（构造参数校验）
- 错误字符串（工具方法返回值）

建议统一为 ApiResponse<T>。

回复：需要处理

### 5. 配置常量化不足
超时、重试次数、最大迭代次数等散落在代码中，应集中到配置类。

回复：需要处理

### 6. 缺少接口抽象
SkillManager、AIAgent 没有接口，难以单元测试和替换实现。

回复：需要处理

### 7. AIFunction 参数类型不明确
Parameters 属性为 object 类型，实际需要 Dictionary，类型转换易出错。

回复：需要处理

### 8. 缺少 Azure OpenAI 支持
OpenAIClient 不支持 Azure 的 api-key header 和 URL 格式。

回复：无需处理

### 9. HTTP 连接池未优化
每次请求新建 HttpWebRequest，未复用连接。

回复：需要处理

### 10. 命名空间组织可优化
大量类放在 NetFrameworkAISDK.Common 下，可按功能分层：Clients、Tools、Messages。

回复：需要处理

---

## 三、安全性问题

### 1. API Key 明文存储
HttpClientBase.ApiKey 作为字符串字段存储，内存转储可能泄露。

回复：无需处理

### 2. 路径遍历保护不足
AgentTools.ValidatePath 仅用 Path.GetFullPath 验证，符号链接等场景不够。

回复：需要处理

---

## 四、性能问题

### 1. BuildSystemPrompt 频繁字符串拼接
每次 AgentLoop 迭代都重建系统提示，应缓存并仅在 Skills 变化时刷新。

回复：需要处理

### 2. SkillManager.EnsureFresh 每次都做文件 I/O
每次访问 Skills 属性都检查目录写入时间，高频场景开销大。

回复：需要处理

---

## 五、修复优先级

### 高优先级（立即）
1. OpenAIClient DeepSeek 硬编码 → 配置化
2. RunCommand 命令注入 → 白名单方式
3. SSE 解析 → 符合规范

### 中优先级（下一版本）
4. TLS 配置 → 运行时检测
5. MergeToolCall 空引用 → 属性初始化
6. 线程安全 → 统一锁保护
7. 错误处理 → 统一为 ApiResponse<T>

### 低优先级（后续优化）
8. 接口抽象
9. CancellationToken 支持
10. 性能优化
