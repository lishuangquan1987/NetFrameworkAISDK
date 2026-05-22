using Newtonsoft.Json;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 多模态内容块，支持文本和图像两种类型。
    /// 文本块序列化为 {"type":"text","text":"..."}，
    /// 图像块序列化为 {"type":"image_url","image_url":{...}}。
    /// </summary>
    public class ImageContentPart
    {
        /// <summary>
        /// 内容类型（"text" 或 "image_url"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容（当 Type 为 "text" 时使用）
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图像详情（当 Type 为 "image_url" 时使用）。
        /// 序列化属性名为 "image_url" 以匹配 OpenAI API 格式。
        /// </summary>
        [JsonProperty("image_url")]
        public ImageDetail Image { get; set; }
    }
}
