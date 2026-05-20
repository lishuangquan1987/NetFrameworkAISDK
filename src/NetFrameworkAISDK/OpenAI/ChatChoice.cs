namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 聊天选择（非流式响应中的单个完成结果）
    /// </summary>
    public class ChatChoice
    {
        /// <summary>
        /// 选择索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 完整的 AI 回复消息
        /// </summary>
        public ChatMessage Message { get; set; }

        /// <summary>
        /// 完成原因（如 "stop"、"tool_calls"、"length"）
        /// </summary>
        public string FinishReason { get; set; }
    }
}