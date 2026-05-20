namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 流式聊天选择（包含增量内容而非完整消息）
    /// </summary>
    public class ChatStreamChoice
    {
        /// <summary>
        /// 选择索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 增量消息（仅包含本次流式分片的新增内容）
        /// </summary>
        public ChatMessage Delta { get; set; }

        /// <summary>
        /// 完成原因（仅最后一个分片设置此值）
        /// </summary>
        public string FinishReason { get; set; }
    }
}