namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 图像源对象。序列化为 {"type":"base64","media_type":"...","data":"..."}
    /// </summary>
    public class ImageSource
    {
        /// <summary>
        /// 源类型（通常为 "base64"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// MIME 类型（如 "image/png", "image/jpeg"）
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// base64 编码的图像数据（不含 data:xxx;base64, 前缀）
        /// </summary>
        public string Data { get; set; }
    }

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
        /// 图像源（image 类型时使用），包含 type/media_type/data 子字段
        /// </summary>
        public ImageSource Source { get; set; }

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

        /// <summary>
        /// 思考/推理内容（thinking 类型内容块时使用）
        /// </summary>
        public string Thinking { get; set; }

        /// <summary>
        /// 思考签名（thinking 类型内容块时使用，需在下一次请求中原样传回）
        /// </summary>
        public string Signature { get; set; }
    }
}