using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI client abstract base with shared tool management.
    /// OpenAIClient and AnthropicClient implement IAIClient directly
    /// and delegate shared logic to this helper.
    /// </summary>
    public abstract class AIClientBase : IAIClient
    {
        protected readonly string ApiKey;
        protected readonly string BaseUrl;
        protected List<AIFunction> _tools;
        protected Dictionary<string, AIFunction> _toolMap;

        protected AIClientBase(string apiKey, string baseUrl)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("apiKey", "API key is required");
            }
            ApiKey = apiKey;
            BaseUrl = baseUrl.TrimEnd('/');
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        public abstract ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options);

        public abstract void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError);

        public virtual void ConfigureTools(IEnumerable<AIFunction> tools)
        {
            _tools = tools != null ? new List<AIFunction>(tools) : new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
            if (tools != null)
            {
                foreach (var t in tools)
                {
                    if (t != null && !string.IsNullOrEmpty(t.Name))
                    {
                        _toolMap[t.Name] = t;
                    }
                }
            }
        }

        protected AIFunction FindTool(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            if (_toolMap.ContainsKey(name))
            {
                return _toolMap[name];
            }
            return null;
        }

        protected string ExecuteTool(string functionName, string functionArgs)
        {
            var function = FindTool(functionName);
            if (function != null)
            {
                return function.Execute(functionArgs);
            }
            return "Error: Tool '" + functionName + "' not found.";
        }

        public virtual void Dispose()
        {
            _tools.Clear();
            _toolMap.Clear();
        }
    }
}