using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 统一对话响应，包含模型返回的文本和工具调用信息
    /// </summary>
    public class ConversationResponse
    {
        /// <summary>
        /// 模型返回的文本内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 本次响应中的工具调用列表（可能为 null 或空列表）
        /// </summary>
        public List<ToolCallRequest> ToolCalls { get; set; }

        /// <summary>
        /// 模型停止原因（如 "stop", "tool_calls", "end_turn", "max_tokens"）
        /// </summary>
        public string FinishReason { get; set; }

        /// <summary>
        /// 推理内容（DeepSeek 思考模式专用）
        /// </summary>
        public string ReasoningContent { get; set; }
    }
}
