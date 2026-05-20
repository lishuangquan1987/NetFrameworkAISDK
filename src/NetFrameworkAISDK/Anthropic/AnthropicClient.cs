using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic API client - implements IAIClient for unified agent usage
    /// </summary>
    public class AnthropicClient : HttpClientBase, IAIClient
    {
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
        private const string ApiVersion = "2023-06-01";
        private List<AIFunction> _tools;
        private Dictionary<string, AIFunction> _toolMap;

        /// <summary>
        /// Constructor with default base URL
        /// </summary>
        public AnthropicClient(string apiKey) : this(apiKey, DefaultBaseUrl)
        {
        }

        /// <summary>
        /// Constructor with custom base URL
        /// </summary>
        public AnthropicClient(string apiKey, string baseUrl) : base(apiKey, baseUrl)
        {
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["anthropic-version"] = ApiVersion;
            request.Headers["x-api-key"] = ApiKey;
        }

        /// <summary>
        /// Create message (non-streaming)
        /// </summary>
        public ApiResponse<MessagesResponse> CreateMessage(
            string model,
            List<AnthropicMessage> messages,
            int maxTokens,
            string system = null,
            double? temperature = null,
            List<ToolDefinition> tools = null)
        {
            var request = new MessagesRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = maxTokens,
                System = system,
                Temperature = temperature,
                Stream = false,
                Tools = tools
            };

            return Post<MessagesResponse>("messages", request);
        }

        /// <summary>
        /// Create message (streaming)
        /// </summary>
        public void CreateMessageStream(
            string model,
            List<AnthropicMessage> messages,
            int maxTokens,
            Action<StreamEvent> onEvent,
            Action<ApiError> onError,
            string system = null,
            double? temperature = null,
            List<ToolDefinition> tools = null)
        {
            var request = new MessagesRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = maxTokens,
                System = system,
                Temperature = temperature,
                Stream = true,
                Tools = tools
            };

            PostStream("messages", request, onEvent, onError);
        }

        // ---- IAIClient implementation ----

        public void ConfigureTools(IEnumerable<AIFunction> tools)
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

        public ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options)
        {
            var anthropicMessages = ConvertToAnthropicMessages(messages);
            var toolDefs = BuildToolDefinitions(options);
            int maxTokens = options.MaxTokens.HasValue ? options.MaxTokens.Value : 1024;

            var response = CreateMessage(
                options.Model,
                anthropicMessages,
                maxTokens,
                options.SystemPrompt,
                options.Temperature,
                toolDefs);

            if (!response.IsSuccess)
            {
                return new ApiResponse<ConversationResponse> { Error = response.Error };
            }

            return new ApiResponse<ConversationResponse>
            {
                Result = ConvertFromAnthropicResponse(response.Result)
            };
        }

        public void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError)
        {
            var anthropicMessages = ConvertToAnthropicMessages(messages);
            var toolDefs = BuildToolDefinitions(options);
            int maxTokens = options.MaxTokens.HasValue ? options.MaxTokens.Value : 1024;

            CreateMessageStream(
                options.Model,
                anthropicMessages,
                maxTokens,
                new Action<StreamEvent>(streamEvent =>
                {
                    var convResp = new ConversationResponse();

                    if (streamEvent.Delta != null && !string.IsNullOrEmpty(streamEvent.Delta.Text))
                    {
                        convResp.Content = streamEvent.Delta.Text;
                    }

                    if (streamEvent.Message != null)
                    {
                        convResp.Model = streamEvent.Message.Model;
                    }

                    if (streamEvent.Type == StreamEventType.MessageDelta
                        && streamEvent.DeltaMessage != null)
                    {
                        convResp.FinishReason = streamEvent.DeltaMessage.StopReason;
                    }

                    onChunk(convResp);
                }),
                onError,
                options.SystemPrompt,
                options.Temperature,
                toolDefs);
        }

        // ---- Private helpers ----

        private List<AnthropicMessage> ConvertToAnthropicMessages(List<ConversationMessage> messages)
        {
            var result = new List<AnthropicMessage>();

            foreach (var msg in messages)
            {
                var role = msg.Role;
                if (role == MessageRole.System)
                {
                    continue;
                }
                if (role == MessageRole.Tool)
                {
                    role = AnthropicRole.User;
                }

                result.Add(new AnthropicMessage
                {
                    Role = role,
                    Content = msg.Content
                });
            }

            return result;
        }

        private List<ToolDefinition> BuildToolDefinitions(ConversationOptions options)
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

        private ConversationResponse ConvertFromAnthropicResponse(MessagesResponse anthropicResponse)
        {
            var result = new ConversationResponse
            {
                Model = anthropicResponse.Model,
                FinishReason = anthropicResponse.StopReason
            };

            if (anthropicResponse.Content != null)
            {
                foreach (var block in anthropicResponse.Content)
                {
                    if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                    {
                        if (result.Content != null)
                        {
                            result.Content = result.Content + block.Text;
                        }
                        else
                        {
                            result.Content = block.Text;
                        }
                    }

                    if (block.Type == "tool_use")
                    {
                        if (result.ToolCalls == null)
                        {
                            result.ToolCalls = new List<ToolCallRequest>();
                        }
                        result.ToolCalls.Add(new ToolCallRequest
                        {
                            Id = block.Id,
                            Type = "function",
                            FunctionName = block.Name,
                            FunctionArguments = block.Input != null ? JsonHelper.Serialize(block.Input) : "{}"
                        });
                    }
                }
            }

            return result;
        }

        private AIFunction FindTool(string name)
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
    }
}