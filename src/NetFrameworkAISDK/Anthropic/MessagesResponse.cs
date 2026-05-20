using System.Collections.Generic;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 消息创建响应
    /// </summary>
    public class MessagesResponse
    {
        /// <summary>
        /// 对象类型（通常为 "message"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 消息唯一标识符
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 角色（通常为 "assistant"）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容块列表
        /// </summary>
        public List<ContentBlock> Content { get; set; }

        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 停止原因（如 "end_turn"、"tool_use"、"max_tokens"）
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// 触发停止的序列
        /// </summary>
        public string StopSequence { get; set; }

        /// <summary>
        /// Token 使用统计
        /// </summary>
        public AnthropicUsage Usage { get; set; }
    }
}