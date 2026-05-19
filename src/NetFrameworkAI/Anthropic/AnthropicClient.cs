using NetFrameworkAI.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAI.Anthropic
{
    /// <summary>
    /// Anthropic API 客户端
    /// </summary>
    public class AnthropicClient : HttpClientBase
    {
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
        private const string ApiVersion = "2023-06-01";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        public AnthropicClient(string apiKey) : this(apiKey, DefaultBaseUrl)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 端点</param>
        public AnthropicClient(string apiKey, string baseUrl) : base(apiKey, baseUrl)
        {
        }

        /// <summary>
        /// 配置请求（添加 Anthropic 特定的 headers）
        /// </summary>
        /// <param name="request">HttpWebRequest 请求对象</param>
        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["anthropic-version"] = ApiVersion;
            request.Headers["x-api-key"] = ApiKey;
        }

        /// <summary>
        /// 创建消息（非流式）
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="maxTokens">最大 token 数</param>
        /// <param name="system">系统提示</param>
        /// <param name="temperature">温度参数</param>
        /// <returns>API 响应</returns>
        public ApiResponse<MessagesResponse> CreateMessage(
            string model,
            List<AnthropicMessage> messages,
            int maxTokens,
            string system = null,
            double? temperature = null)
        {
            var request = new MessagesRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = maxTokens,
                System = system,
                Temperature = temperature,
                Stream = false
            };

            return Post<MessagesResponse>("messages", request);
        }

        /// <summary>
        /// 创建消息（流式，使用 Action<T> 回调）
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="maxTokens">最大 token 数</param>
        /// <param name="onEvent">事件回调</param>
        /// <param name="onError">错误回调</param>
        /// <param name="system">系统提示</param>
        /// <param name="temperature">温度参数</param>
        public void CreateMessageStream(
            string model,
            List<AnthropicMessage> messages,
            int maxTokens,
            Action<StreamEvent> onEvent,
            Action<ApiError> onError,
            string system = null,
            double? temperature = null)
        {
            var request = new MessagesRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = maxTokens,
                System = system,
                Temperature = temperature,
                Stream = true
            };

            PostStream("messages", request, onEvent, onError);
        }
    }
}
