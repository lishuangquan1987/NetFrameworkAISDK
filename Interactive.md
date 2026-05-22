# Interactive.md - 项目问题与讨论

## 关于项目的问题

1. **命名空间/项目结构**：项目的主要命名空间是什么？项目结构如何规划？（例如：是否分为 OpenAI、Anthropic、Common 等文件夹/项目？）

回复：命名空间：**NetFrameworkAISDK**，**NetFrameworkAISDK.OpenAI**,**NetFrameworkAISDK.Anthropic**等等

1. **Flurl 版本**：原计划使用 Flurl 进行 HTTP 请求，但 .NET Framework 4.0 兼容的 Flurl 版本受限。
> 最终方案：使用原生 HttpWebRequest（已从 AGENTS.md 移除 Flurl 要求）。

2. **JSON 序列化库**：要求将 JSON 封装为类，拒绝字符串拼接。那么使用哪个 JSON 序列化库？（Newtonsoft.Json 是 .NET Framework 4.0 时代最常用的选择）

回复：Newtonsoft.Json

1. **工具调用的实现方式**：README 中展示了类似 Microsoft Agent Framework 的工具调用方式。我们是否需要完全照搬该框架的设计，还是可以有自己的简化实现？

   回复：可以简化，但是最好简单易用

2. **流式回复的实现**：对于 .NET Framework 4.0，`await foreach`（C# 8.0 的 IAsyncEnumerable）不可用。我们如何实现流式回复 API？是否需要使用回调方式或其他兼容方案？

   回复：使用Action<T>

3. **项目类型**：这是一个类库项目（Class Library）还是控制台应用程序？

   回复：作为一个开源的SDK，你认为是什么程序？？？

4. **API 密钥管理**：如何处理 API 密钥和认证？是通过构造函数传入，还是有其他配置方式？

   回复：构造函数传入，SDK层不管API秘钥、url/模型从哪里读取，只需要传过来就行

5. **错误处理**：对于 API 调用失败的情况，错误处理策略是什么？（抛出异常、返回错误对象等）

   回复：返回错误对象

6. **单元测试**：是否需要编写单元测试？如果需要，使用什么测试框架？

   回复：需要，从支持.net framework4.0最流行的测试框架选一个

7. **Anthropic 模型**：README 提到了 OpenAI 和 Anthropic，但示例代码只展示了 OpenAI。Anthropic 的 API 协议是否已经明确？

   回复：Anthropic 的API协议可以参考：https://github.com/anthropics/anthropic-sdk-csharp
