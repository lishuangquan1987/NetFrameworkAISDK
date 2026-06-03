using System;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 客户端配置选项，允许自定义特定提供商的行为。
    /// </summary>
    public class OpenAIClientOptions
    {
        /// <summary>
        /// 是否启用 DeepSeek 兼容模式（默认开启）。
        /// 启用后会：
        /// - assistant 消息带 tool_calls 时自动设置 name 字段为 "assistant"
        /// - 在后续消息中保留 reasoning_content 字段
        /// </summary>
        public bool EnableDeepSeekCompatibility { get; set; }

        /// <summary>
        /// 是否支持 reasoning_content 字段（默认开启，与 DeepSeek 兼容保持一致）。
        /// 某些模型（如 DeepSeek-R1）会返回思考过程，需要在对话中保留。
        /// </summary>
        public bool SupportReasoningContent { get; set; }

        /// <summary>
        /// 创建默认配置（DeepSeek 兼容模式默认开启）。
        /// </summary>
        public static OpenAIClientOptions Default
        {
            get
            {
                return new OpenAIClientOptions
                {
                    EnableDeepSeekCompatibility = true,
                    SupportReasoningContent = true
                };
            }
        }

        /// <summary>
        /// 根据 baseUrl 自动检测并创建合适的配置。
        /// </summary>
        public static OpenAIClientOptions DetectFromBaseUrl(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return Default;
            }

            var lowerUrl = baseUrl.ToLowerInvariant();
            if (lowerUrl.Contains("deepseek"))
            {
                return Default;
            }

            return Default;
        }
    }
}
