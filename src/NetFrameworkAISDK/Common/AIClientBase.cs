using System;
using System.Collections.Generic;
using NetFrameworkAISDK.OpenAI;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI client abstract base class providing shared tool management
    /// and HTTP infrastructure via HttpClientBase.
    /// </summary>
    public abstract class AIClientBase : HttpClientBase, IAIClient
    {
        protected List<AIFunction> _tools;
        protected Dictionary<string, AIFunction> _toolMap;

        protected AIClientBase(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        protected AIClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
            : base(apiKey, baseUrl, timeoutMilliseconds)
        {
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

        protected List<ToolDefinition> BuildToolDefinitions(ConversationOptions options)
        {
            var allTools = new List<AIFunction>();
            if (_tools != null)
            {
                allTools.AddRange(_tools);
            }
            if (options.Tools != null)
            {
                allTools.AddRange(options.Tools);
            }

            if (allTools.Count == 0)
            {
                return null;
            }

            var toolDefs = new List<ToolDefinition>();
            foreach (var t in allTools)
            {
                if (t != null)
                {
                    toolDefs.Add(t.ToToolDefinition());
                }
            }

            if (toolDefs.Count == 0)
            {
                return null;
            }

            return toolDefs;
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

        public string ExecuteTool(string functionName, string functionArgs)
        {
            var function = FindTool(functionName);
            if (function != null)
            {
                return function.Execute(functionArgs);
            }
            return "Error: Tool '" + functionName + "' not found.";
        }

        public override void Dispose()
        {
            _tools.Clear();
            _toolMap.Clear();
            base.Dispose();
        }
    }
}