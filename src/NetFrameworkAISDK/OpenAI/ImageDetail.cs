namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// 图像详情，包含图像数据的 URL 或 base64 编码
    /// </summary>
    public class ImageDetail
    {
        /// <summary>
        /// 图像 URL 或 base64 数据（格式：data:image/png;base64,...）
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 图像格式（url 或 base64）
        /// </summary>
        public string Detail { get; set; }
    }
}