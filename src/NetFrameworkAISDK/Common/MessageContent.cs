using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 统一的消息内容类型
    /// </summary>
    public class MessageContent
    {
        /// <summary>
        /// 内容类型
        /// </summary>
        public ContentType Type { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图像 URL
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// base64 编码的图像数据
        /// </summary>
        public string ImageBase64 { get; set; }

        /// <summary>
        /// 图像媒体类型（用于 Anthropic）
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// 图像详情级别（low/high，用于 OpenAI）
        /// </summary>
        public string Detail { get; set; }
    }

    /// <summary>
    /// 内容类型枚举
    /// </summary>
    public enum ContentType
    {
        Text,
        Image
    }

    /// <summary>
    /// MessageContent 帮助类
    /// </summary>
    public static class MessageContentHelper
    {
        /// <summary>
        /// 创建文本内容
        /// </summary>
        public static MessageContent CreateText(string text)
        {
            return new MessageContent { Type = ContentType.Text, Text = text };
        }

        /// <summary>
        /// 创建图像内容（通过 URL）
        /// </summary>
        public static MessageContent CreateImageUrl(string url, string detail = null)
        {
            return new MessageContent 
            { 
                Type = ContentType.Image, 
                ImageUrl = url,
                Detail = detail
            };
        }

        /// <summary>
        /// 创建图像内容（通过 base64）
        /// </summary>
        public static MessageContent CreateImageBase64(string base64Data, string mediaType = "image/png", string detail = null)
        {
            return new MessageContent
            {
                Type = ContentType.Image,
                ImageBase64 = base64Data,
                MediaType = mediaType,
                Detail = detail
            };
        }
    }
}
