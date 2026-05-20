using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI client unified interface - provider-agnostic
    /// </summary>
    public interface IAIClient : IDisposable
    {
        /// <summary>
        /// Send a conversation and get a response (non-streaming)
        /// </summary>
        ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options);

        /// <summary>
        /// Send a conversation and get streaming responses
        /// </summary>
        void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError);

        /// <summary>
        /// Configure available tools
        /// </summary>
        void ConfigureTools(IEnumerable<AIFunction> tools);
    }
}