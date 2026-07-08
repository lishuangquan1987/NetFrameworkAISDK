namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// OpenAI 推理努力程度常量。
    /// 用于 <see cref="ConversationOptions.ThinkingEffort"/>，控制 o-series 模型的推理深度。
    /// </summary>
    public static class ThinkingEffort
    {
        /// <summary>低努力（最快，推理最少）</summary>
        public const string Low = "low";

        /// <summary>中等努力（默认平衡）</summary>
        public const string Medium = "medium";

        /// <summary>高努力（最慢，推理最深入）</summary>
        public const string High = "high";
    }
}
