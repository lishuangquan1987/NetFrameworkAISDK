using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// AIAgent for OpenAI - delegates to Common.AIAgent via IAIClient.
    /// Preferred: use new Common.AIAgent(client, model, instructions, tools) directly.
    /// </summary>
    public class AIAgent
    {
        private readonly Common.AIAgent _inner;

        public AIAgent(OpenAIClient client, string model, string instructions, IEnumerable<AIFunction> tools)
        {
            _inner = new Common.AIAgent(client, model, instructions, tools);
        }

        public void AddTool(AIFunction function)
        {
            _inner.AddTool(function);
        }

        public ApiResponse<string> Run(string userMessage, Action<string, string, string> onToolCall = null)
        {
            return _inner.Run(userMessage, onToolCall);
        }

        public void RunStreaming(string userMessage, Action<string> onUpdate, Action<ApiError> onError, Action<string, string, string> onToolCall = null)
        {
            _inner.RunStreaming(userMessage, onUpdate, onError, onToolCall);
        }

        public void ClearHistory()
        {
            _inner.ClearHistory();
        }
    }
}