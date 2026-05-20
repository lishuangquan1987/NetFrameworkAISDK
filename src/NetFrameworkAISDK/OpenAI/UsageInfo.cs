namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI API 使用统计（Token 计数）
    /// </summary>
    public class UsageInfo
    {
        /// <summary>
        /// 提示词消耗的 token 数
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// 生成回复消耗的 token 数
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// 总计消耗的 token 数
        /// </summary>
        public int TotalTokens { get; set; }
    }
}