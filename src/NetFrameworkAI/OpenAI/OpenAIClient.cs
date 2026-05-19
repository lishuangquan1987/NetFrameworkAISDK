using NetFrameworkAI.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAI.OpenAI
{
    /// <summary>
    /// OpenAI API 客户端
    /// </summary>
    public class OpenAIClient : HttpClientBase
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        public OpenAIClient(string apiKey) : this(apiKey, DefaultBaseUrl)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 端点</param>
        public OpenAIClient(string apiKey, string baseUrl) : base(apiKey, baseUrl)
        {
        }

        /// <summary>
        /// 配置请求（添加 OpenAI 特定的请求头
        /// </summary>
        /// <param name="request">HttpWebRequest 请求对象</param>
        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["Authorization"] = "Bearer " + ApiKey;
        }

        /// <summary>
        /// 创建聊天完成（非流式）
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="temperature">温度参数</param>
        /// <param name="maxTokens">最大 token 数</param>
        /// <param name="tools">工具列表</param>
        /// <returns>API 响应</returns>
        public ApiResponse<ChatCompletionResponse> CreateChatCompletion(
            string model,
            List<ChatMessage> messages,
            double? temperature = null,
            int? maxTokens = null,
            List<ToolDefinition> tools = null)
        {
            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Temperature = temperature,
                MaxTokens = maxTokens,
                Tools = tools,
                Stream = false
            };

            return Post<ChatCompletionResponse>("chat/completions", request);
        }

        /// <summary>
        /// 创建聊天完成（流式，使用 Action<T> 回调）
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="onData">数据回调</param>
        /// <param name="onError">错误回调</param>
        /// <param name="temperature">温度参数</param>
        /// <param name="maxTokens">最大 token 数</param>
        /// <param name="tools">工具列表</param>
        public void CreateChatCompletionStream(
            string model,
            List<ChatMessage> messages,
            Action<ChatCompletionStreamResponse> onData,
            Action<ApiError> onError,
            double? temperature = null,
            int? maxTokens = null,
            List<ToolDefinition> tools = null)
        {
            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Temperature = temperature,
                MaxTokens = maxTokens,
                Tools = tools,
                Stream = true
            };

            PostStream("chat/completions", request, onData, onError);
        }
    }
}
