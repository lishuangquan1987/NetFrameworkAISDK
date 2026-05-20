# NetFrameworkAISDK - AI 代理开发规范

## 项目基础信息
- **项目名称**：NetFrameworkAISDK
- **项目类型**：类库项目（Class Library）
- **主要命名空间**：
  - NetFrameworkAISDK - 核心命名空间
  - NetFrameworkAISDK.OpenAI - OpenAI 相关功能
  - NetFrameworkAISDK.Anthropic - Anthropic 相关功能
- **目标框架**：.NET Framework 4.0+

## 技术栈约束
- .NET Framework 4.0+ 兼容性：禁止使用 C# 6.0+ 特性
- HTTP 请求：必须使用 Flurl 库
- JSON 处理：使用 Newtonsoft.Json + SnakeCaseNamingStrategy

## JSON 序列化模式
必须使用 SnakeCaseNamingStrategy：
`csharp
new JsonSerializerSettings {
    ContractResolver = new DefaultContractResolver {
        NamingStrategy = new SnakeCaseNamingStrategy()
    }
};
`

## TLS 1.2 配置
`csharp
ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
`

## HTTP 请求模式
使用 Flurl.Url 构建 URL：
`csharp
var url = new Url(BaseUrl.ToString()).AppendPathSegment(endpoint);
`

## 工具调用 Agent 流程
检查 assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0

## SkillManager - MAF 渐进式披露模式
禁止一次性加载所有 Skill 内容到 system prompt
正确流程：DiscoverSkills -> BuildProgressivePrompt -> CreateLoadSkillFunction

## C# 4.0 兼容性检查清单
- obj?.Prop -> if (obj != null)
-  -> string.Format
- nameof -> 直接写字符串

## 验证标准
1. 代码兼容 .NET Framework 4.0
2. HTTP 请求使用 Flurl
3. JSON 使用强类型类 + SnakeCaseNamingStrategy
4. 遵循现有代码风格
5. 无未请求的功能
6. 公共 API 有文档注释
7. API 密钥通过构造函数传入
8. 流式回复使用 Action<T> 回调
9. 错误处理返回错误对象
10. 命名空间使用 NetFrameworkAISDK.* 格式

## 参考资料
- Anthropic SDK: github.com/anthropics/anthropic-sdk-csharp
- OpenAI SDK: github.com/openai/openai-dotnet
- Microsoft Agent Framework: github.com/microsoft/agent-framework
