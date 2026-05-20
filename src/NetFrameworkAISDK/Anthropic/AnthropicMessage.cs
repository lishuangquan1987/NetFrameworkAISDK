using System.Collections.Generic;

namespace NetFrameworkAISDK.Anthropic
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
        /// 类型（"text", "image", "tool_use"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图像数据（base64 编码）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 图像媒体类型
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// 工具使用 ID（tool_use 类型时使用）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 工具名称（tool_use 类型时使用）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具输入参数（tool_use 类型时使用）
        /// </summary>
        public object Input { get; set; }
    }
}
