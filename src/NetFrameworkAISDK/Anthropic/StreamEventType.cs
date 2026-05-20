namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 流式事件类型常量
    /// </summary>
    public static class StreamEventType
    {
        /// <summary>消息开始事件</summary>
        public const string MessageStart = "message_start";

        /// <summary>内容块开始事件</summary>
        public const string ContentBlockStart = "content_block_start";

        /// <summary>内容块增量事件</summary>
        public const string ContentBlockDelta = "content_block_delta";

        /// <summary>内容块结束事件</summary>
        public const string ContentBlockStop = "content_block_stop";

        /// <summary>消息增量事件</summary>
        public const string MessageDelta = "message_delta";

        /// <summary>消息结束事件</summary>
        public const string MessageStop = "message_stop";
    }
}