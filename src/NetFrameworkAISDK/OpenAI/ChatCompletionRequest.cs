using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 聊天完成请求
    /// </summary>
    public class ChatCompletionRequest
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 消息列表
        /// </summary>
        public List<ChatMessage> Messages { get; set; }

        /// <summary>
        /// 是否启用流式输出
        /// </summary>
        public bool? Stream { get; set; }

        /// <summary>
        /// 温度参数（0-2），控制回复随机性
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// 最大生成 token 数
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// 可用工具列表
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }

        /// <summary>
        /// 响应格式配置（结构化输出），为 null 时不启用
        /// </summary>
        public OpenAiResponseFormat ResponseFormat { get; set; }

        /// <summary>
        /// 推理努力程度（OpenAI o-series 模型支持）。
        /// 可选值："low"、"medium"、"high"。
        /// 为 null 时不发送该参数，模型使用默认行为。
        /// </summary>
        public string ReasoningEffort { get; set; }
    }
}