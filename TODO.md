# NetFrameworkAISDK 整改任务清单

## 阶段一：项目重命名 [x]
- [x] 1.1 重命名 NetFrameworkAI.sln -> NetFrameworkAISDK.sln
- [x] 1.2 重命名 src/NetFrameworkAI/ -> src/NetFrameworkAISDK/
- [x] 1.3 更新所有 .csproj 的 RootNamespace 和 AssemblyName
- [x] 1.4 更新所有 .cs 文件的命名空间 NetFrameworkAI -> NetFrameworkAISDK (16个文件)
- [x] 1.5 更新 samples 目录及命名空间
- [x] 1.6 更新 tests 目录及命名空间
- [x] 1.7 更新 packages.config 引用路径 (无变化)

## 阶段二：安全修复 [x]
- [x] 2.1 HTTP 超时配置 (30秒) - HttpClientBase.cs
- [x] 2.2 API Key 验证 (构造函数空值检查) - HttpClientBase.cs
- [x] 2.3 命令注入漏洞修复 (AgentTools.RunCommand) - 添加危险字符检查
- [x] 2.4 空指针安全检查 (HttpClientBase.PostStream) - response.GetResponseStream() 检查
- [x] 2.5 静态初始化异常处理 (HttpClientBase) - 添加 try-catch
- [x] 2.6 SkillManager 空 catch 块修复 - 添加异常日志

## 阶段三：多模态图像支持 [x]
- [x] 3.1 创建统一的 MessageContent 内容块类型 (Common/MessageContent.cs)
- [x] 3.2 扩展 OpenAI ChatMessage 支持多内容 (ImageContentPart, ImageDetail)
- [x] 3.3 扩展 Anthropic 内容块支持图像 (Source, MediaType)
- [x] 3.4 更新各自 Client 的序列化逻辑 (已完成 — 修复文本块误用 Image.Url)

## 阶段四：SkillManager 重构 [x]
- [x] 4.1 提取公共方法，消除 CreateLoadSkillFunction/CreateReadSkillTool 重复代码
- [x] 4.2 统一方法命名语义 (CreateSkillFunction, ExtractSkillName, FindSkill)
- [x] 4.3 添加异常日志记录 (已在阶段二完成)
- [x] 4.4 修复大小写敏感匹配 (使用 StringComparison.OrdinalIgnoreCase)

## 阶段五：代码质量提升 [x]
- [x] 5.1 添加缺失的 XML 文档注释 (公共 API)
- [x] 5.2 McpClient 超时机制 - 线程安全修复
- [x] 5.3 工具函数查找优化 (O(n) -> O(1) 使用 Dictionary) - AIAgent.cs
- [x] 5.4 Substring 边界检查修复 (HttpClientBase.PostStream) - 已在阶段二完成

## 阶段六：统一抽象层设计 [x]
- [x] 6.1 创建 IAIClient 接口 (Common/IAIClient.cs)
- [x] 6.2 创建 AIClientBase 抽象基类 (Common/AIClientBase.cs)
- [x] 6.3 重构 OpenAIClient 继承 AIClientBase
- [x] 6.4 重构 AnthropicClient 继承 AIClientBase

## 阶段七：架构优化 (2026-05)
- [x] 7.1 修复 ImageContentPart 文本/图片语义混淆 (OpenAIClient)
- [x] 7.2 修复 AIFunctionFactory.InvokeMethod 复杂类型参数转换
- [x] 7.3 迁移 ToolDefinition/FunctionDefinition 到 Common 命名空间，消除跨命名空间耦合
- [x] 7.4 修复 JsonSchemaGenerator 属性名 PascalCase → snake_case
- [x] 7.5 统一使用 JsonHelper 替代裸 JsonConvert
- [x] 7.6 消除 SkillManager/SkillFunctionHandler FindSkill 代码重复
- [x] 7.7 OpenAIClient 流式对话新增 ResponseFormat 支持
- [x] 7.8 AIAgent.MaxIterations 可配置化

## 阶段八：SkillManager 实例化重构 (2026-05-22)
- [x] 8.1 SkillManager static → 实例类（持有目录路径、技能列表、最后扫描时间）
- [x] 8.2 SkillFunctionHandler 删除，LoadSkill/ReadSkill 合入 SkillManager 实例方法
- [x] 8.3 AIAgent 移除 _skills/_skillsDirectories，改用 _skillManager 字段
- [x] 8.4 新增 EnsureFresh() 文件变更自检 + Refresh()/AddDirectory() API
- [x] 8.5 AIAgent 新增 SetModel() 方法和 SkillManager 只读属性
- [x] 8.6 更新 Samples / Tests 适配实例 API
- [x] 8.7 清理死代码（AIClientBase.ExecuteTool/FindTool, McpJsonRpcResponse.HasError, SkillInfo.DirectoryPath/Files 枚举）
- [x] 8.8 补全 Anthropic 流式 text 重复发送、image source 结构、流式 structured output 支持
- [x] 8.9 AIAgent 流式路径工具审批补全 + 空历史索引保护
- [x] 8.10 AgentTools Glob 重写 + RunCommand 黑名单扩展
- [x] 8.11 文档全量同步（ProjectRules/Interactive/AGENTS/README/tasks/TODO）

---
所有任务已完成！

---
最后更新: 2026-05-22