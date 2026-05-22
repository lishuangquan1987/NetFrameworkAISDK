# 项目执行任务计划

## 阶段一：项目初始化
1. 创建 .NET Framework 4.0 类库项目 NetFrameworkAISDK
2. 创建项目文件夹结构（OpenAI、Anthropic、Common）
3. 配置 NuGet 依赖：Newtonsoft.Json
4. 创建解决方案文件

## 阶段二：核心基础设施
5. 创建通用 HTTP 客户端基类（使用 HttpWebRequest）
6. 创建 JSON 序列化/反序列化辅助类（使用 Newtonsoft.Json）
7. 创建错误对象基类和通用错误类型
8. 创建项目级 AssemblyInfo.cs

## 阶段三：OpenAI 支持
9. 研究 OpenAI API 协议（参考 openai-dotnet SDK）
10. 创建 OpenAI 请求/响应模型类
11. 实现 OpenAIClient 类（构造函数传入 API 密钥和端点）
12. 实现基础聊天完成 API（非流式）
13. 实现流式聊天完成 API（使用 Action<T> 回调）
14. 实现工具调用支持（简化版）
15. 创建 AIAgent 类或类似的高层封装

## 阶段四：Anthropic 支持
16. 研究 Anthropic API 协议（参考 anthropic-sdk-csharp）
17. 创建 Anthropic 请求/响应模型类
18. 实现 AnthropicClient 类
19. 实现基础消息 API（非流式）
20. 实现流式消息 API（使用 Action<T> 回调）

## 阶段五：测试与文档
21. 选择并配置 .NET Framework 4.0 兼容的测试框架
22. 编写核心基础设施单元测试
23. 编写 OpenAI 功能单元测试
24. 编写 Anthropic 功能单元测试
25. 完善 XML 文档注释
26. 更新 README.MD 添加使用示例

## 验证清单
每个任务完成后验证：
- ✅ 代码兼容 .NET Framework 4.0
- ✅ HTTP 请求使用 HttpWebRequest
- ✅ JSON 处理使用强类型类
- ✅ API 密钥通过构造函数传入
- ✅ 流式回复使用 Action<T> 回调
- ✅ 错误处理返回错误对象
- ✅ 命名空间使用 NetFrameworkAISDK.* 格式
