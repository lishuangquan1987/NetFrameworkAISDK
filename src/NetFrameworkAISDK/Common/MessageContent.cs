namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 多模态消息内容块，支持文本和图像
    /// </summary>
    public class MessageContent
    {
        /// <summary>
        /// 内容类型（参见 <see cref="ContentType"/> 常量）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容（当 Type 为 Text 时有效）
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图像 URL（当 Type 为 Image 时有效）
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// base64 编码的图像数据（当 Type 为 Image 时有效）
        /// </summary>
        public string ImageBase64 { get; set; }

        /// <summary>
        /// 图像 MIME 类型（如 "image/png"，当 Type 为 Image 时有效）
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// 图像细节级别（如 "high"、"low"、"auto"）
        /// </summary>
        public string Detail { get; set; }
    }
}