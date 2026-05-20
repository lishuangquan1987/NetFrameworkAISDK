namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 内容块，支持文本、图像、工具调用和工具结果
    /// </summary>
    public class ContentBlock
    {
        /// <summary>
        /// 内容类型（"text"、"image"、"tool_use"、"tool_result"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容（text 类型时使用）
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 工具调用结果内容（tool_result 类型时使用）
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 图像数据（base64 编码，image 类型时使用）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 图像媒体类型（image 类型时使用，如 "image/jpeg"）
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// 工具使用唯一标识符（tool_use / tool_result 类型时使用）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 关联的工具调用 ID（tool_result 类型时使用）
        /// </summary>
        public string ToolUseId { get; set; }

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