namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic SSE 流式事件，包含消息、内容块和增量数据
    /// </summary>
    public class StreamEvent
    {
        /// <summary>
        /// 事件类型（参见 <see cref="StreamEventType"/> 常量）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 消息数据（message_start 事件时使用）
        /// </summary>
        public MessagesResponse Message { get; set; }

        /// <summary>
        /// 索引（content_block_* 事件时使用）
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// 内容块（content_block_start 事件时使用）
        /// </summary>
        public ContentBlock ContentBlock { get; set; }

        /// <summary>
        /// 增量数据（content_block_delta / message_delta 事件时使用）
        /// </summary>
        public Delta Delta { get; set; }

        /// <summary>
        /// 使用统计增量（message_delta 事件时使用）
        /// </summary>
        public AnthropicUsage Usage { get; set; }
    }
}