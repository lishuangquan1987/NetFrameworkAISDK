namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 图像内容块，用于多模态消息中的图像传输
    /// </summary>
    public class ImageContentPart
    {
        /// <summary>
        /// 内容类型（通常为 "image_url"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 图像详情
        /// </summary>
        public ImageDetail Image { get; set; }
    }
}