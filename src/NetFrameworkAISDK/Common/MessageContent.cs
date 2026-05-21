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

        /// <summary>
        /// 从 URL 创建图像内容块
        /// </summary>
        /// <param name="imageUrl">图像 URL</param>
        /// <param name="detail">细节级别（可选，"high"/"low"/"auto"）</param>
        /// <returns>图像内容块</returns>
        public static MessageContent CreateImageFromUrl(string imageUrl, string detail = null)
        {
            return new MessageContent
            {
                Type = ContentType.Image,
                ImageUrl = imageUrl,
                Detail = detail
            };
        }

        /// <summary>
        /// 从 base64 编码数据创建图像内容块
        /// </summary>
        /// <param name="base64Data">base64 编码的图像数据</param>
        /// <param name="mediaType">MIME 类型（默认 "image/png"）</param>
        /// <returns>图像内容块</returns>
        public static MessageContent CreateImageFromBase64(string base64Data, string mediaType = "image/png")
        {
            return new MessageContent
            {
                Type = ContentType.Image,
                ImageBase64 = base64Data,
                MediaType = mediaType
            };
        }

        /// <summary>
        /// 创建文本内容块
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <returns>文本内容块</returns>
        public static MessageContent CreateText(string text)
        {
            return new MessageContent
            {
                Type = ContentType.Text,
                Text = text
            };
        }
    }
}