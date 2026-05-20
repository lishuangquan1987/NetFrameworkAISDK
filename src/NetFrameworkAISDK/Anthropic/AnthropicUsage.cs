namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic API 使用统计（输入/输出 Token 计数）
    /// </summary>
    public class AnthropicUsage
    {
        /// <summary>
        /// 输入 token 数
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// 输出 token 数
        /// </summary>
        public int OutputTokens { get; set; }
    }
}