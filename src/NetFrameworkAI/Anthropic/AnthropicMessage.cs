using System.Collections.Generic;

namespace NetFrameworkAI.Anthropic
{
    /// <summary>
    /// Anthropic 消息角色
    /// </summary>
    public static class AnthropicRole
    {
        public const string User = "user";
        public const string Assistant = "assistant";
    }

    /// <summary>
    /// Anthropic 消息
    /// </summary>
    public class AnthropicMessage
    {
        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容（可以是字符串或内容块列表）
        /// </summary>
        public object Content { get; set; }
    }

    /// <summary>
    /// 内容块
    /// </summary>
    public class ContentBlock
    {
        /// <summary>
        /// 类型（通常是 "text"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        public string Text { get; set; }
    }
}
