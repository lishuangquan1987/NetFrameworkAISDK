namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 思考/扩展思考（Extended Thinking）配置块。
    /// 启用时包含 type="enabled" 和 budget_tokens；禁用时仅 type="disabled"。
    /// </summary>
    public class ThinkingBlock
    {
        /// <summary>
        /// 思考模式类型："enabled" 或 "disabled"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 思考预算 Token 数（仅 type="enabled" 时设置，必须大于 1024）
        /// </summary>
        public int? BudgetTokens { get; set; }
    }
}
