namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 消息，支持文本和多模态内容
    /// </summary>
    public class AnthropicMessage
    {
        /// <summary>
        /// 角色（参见 <see cref="AnthropicRole"/> 常量）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容（可以是纯文本字符串或 <see cref="ContentBlock"/> 列表）
        /// </summary>
        public object Content { get; set; }
    }
}