using System;
using System.Collections.Generic;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI 客户端统一接口，屏蔽 OpenAI/Anthropic 等不同后端的差异。
    /// 所有 AI 客户端必须实现此接口。
    /// </summary>
    public interface IAIClient : IDisposable
    {
        /// <summary>
        /// 发送对话并获取非流式响应
        /// </summary>
        /// <param name="messages">对话消息列表</param>
        /// <param name="options">对话配置选项</param>
        /// <param name="cancellationToken">取消令牌（可选），触发时中止请求</param>
        /// <returns>包含 AI 回复和工具调用信息的响应</returns>
        ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options,
            CancellationToken? cancellationToken = null);

        /// <summary>
        /// 发送对话并获取流式响应
        /// </summary>
        /// <param name="messages">对话消息列表</param>
        /// <param name="options">对话配置选项</param>
        /// <param name="onChunk">每收到一个响应块时的回调</param>
        /// <param name="onError">发生错误时的回调</param>
        /// <param name="cancellationToken">取消令牌（可选），触发时中止请求</param>
        void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError,
            CancellationToken? cancellationToken = null);

        /// <summary>
        /// 配置可用的工具函数列表
        /// </summary>
        /// <param name="tools">AI 函数列表</param>
        void ConfigureTools(IEnumerable<AIFunction> tools);
    }
}