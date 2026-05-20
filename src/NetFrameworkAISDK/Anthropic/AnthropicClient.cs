using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic API client
    /// </summary>
    public class AnthropicClient : AIClientBase
    {
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
        private const string ApiVersion = "2023-06-01";

        public AnthropicClient(string apiKey)
            : this(apiKey, DefaultBaseUrl)
        {
        }

        public AnthropicClient(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
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

        public override ApiResponse<ConversationResponse> SendConversation(
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

        public override void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError)
        {
            var anthropicMessages = ConvertToAnthropicMessages(messages);
            var toolDefs = BuildToolDefinitions(options);
            int maxTokens = options.MaxTokens.HasValue ? options.MaxTokens.Value : 1024;

            var contentBlockStates = new Dictionary<int, ContentBlockState>();
            string modelName = null;

            CreateMessageStream(
                options.Model,
                anthropicMessages,
                maxTokens,
                new Action<StreamEvent>(streamEvent =>
                {
                    var convResp = new ConversationResponse();

                    if (streamEvent.Message != null && streamEvent.Type == StreamEventType.MessageStart)
                    {
                        modelName = streamEvent.Message.Model;
                        convResp.Model = modelName;
                    }

                    if (streamEvent.ContentBlock != null
                        && streamEvent.Type == StreamEventType.ContentBlockStart)
                    {
                        var block = streamEvent.ContentBlock;
                        var index = streamEvent.Index.HasValue ? streamEvent.Index.Value : 0;

                        var state = new ContentBlockState
                        {
                            Type = block.Type,
                            Id = block.Id,
                            Name = block.Name
                        };
                        contentBlockStates[index] = state;
                    }

                    if (streamEvent.Delta != null
                        && streamEvent.Type == StreamEventType.ContentBlockDelta)
                    {
                        var index = streamEvent.Index.HasValue ? streamEvent.Index.Value : 0;
                        ContentBlockState state;
                        if (!contentBlockStates.TryGetValue(index, out state))
                        {
                            state = new ContentBlockState { Type = "text" };
                            contentBlockStates[index] = state;
                        }

                        if (state.Type == "text" && !string.IsNullOrEmpty(streamEvent.Delta.Text))
                        {
                            state.TextBuilder.Append(streamEvent.Delta.Text);
                            convResp.Content = streamEvent.Delta.Text;
                        }
                        else if (state.Type == "tool_use" && !string.IsNullOrEmpty(streamEvent.Delta.PartialJson))
                        {
                            state.TextBuilder.Append(streamEvent.Delta.PartialJson);
                        }
                    }

                    if (streamEvent.Type == StreamEventType.ContentBlockStop)
                    {
                        var index = streamEvent.Index.HasValue ? streamEvent.Index.Value : 0;
                        ContentBlockState state;
                        if (contentBlockStates.TryGetValue(index, out state))
                        {
                            if (state.Type == "tool_use")
                            {
                                var toolCall = new ToolCallRequest
                                {
                                    Id = state.Id,
                                    Type = "function",
                                    FunctionName = state.Name,
                                    FunctionArguments = state.TextBuilder.ToString()
                                };

                                if (convResp.ToolCalls == null)
                                {
                                    convResp.ToolCalls = new List<ToolCallRequest>();
                                }
                                convResp.ToolCalls.Add(toolCall);

                                if (string.IsNullOrEmpty(convResp.Content))
                                {
                                    convResp.Content = null;
                                }
                            }
                            else if (state.Type == "text")
                            {
                                convResp.Content = state.TextBuilder.ToString();
                            }
                        }
                    }

                    if (streamEvent.Type == StreamEventType.MessageDelta
                        && streamEvent.Delta != null)
                    {
                        convResp.FinishReason = streamEvent.Delta.StopReason;
                    }

                    if (streamEvent.Type == StreamEventType.MessageStop)
                    {
                        convResp.FinishReason = "end_turn";
                    }

                    if (convResp.Content != null || convResp.ToolCalls != null)
                    {
                        onChunk(convResp);
                    }
                }),
                onError,
                options.SystemPrompt,
                options.Temperature,
                toolDefs);
        }

        private class ContentBlockState
        {
            public string Type;
            public string Id;
            public string Name;
            public readonly System.Text.StringBuilder TextBuilder = new System.Text.StringBuilder();
        }

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
                    var toolContent = new List<ContentBlock>();
                    toolContent.Add(new ContentBlock
                    {
                        Type = "tool_result",
                        ToolUseId = msg.ToolCallId,
                        Content = msg.Content
                    });
                    result.Add(new AnthropicMessage
                    {
                        Role = MessageRole.User,
                        Content = toolContent
                    });
                    continue;
                }

                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    var blocks = new List<ContentBlock>();
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        blocks.Add(new ContentBlock
                        {
                            Type = "text",
                            Text = msg.Content
                        });
                    }
                    foreach (var tc in msg.ToolCalls)
                    {
                        object input = new Dictionary<string, object>();
                        if (!string.IsNullOrEmpty(tc.FunctionArguments))
                        {
                            try
                            {
                                input = JsonHelper.Deserialize<object>(tc.FunctionArguments);
                            }
                            catch
                            {
                                input = new Dictionary<string, object>();
                            }
                        }
                        blocks.Add(new ContentBlock
                        {
                            Type = "tool_use",
                            Id = tc.Id,
                            Name = tc.FunctionName,
                            Input = input
                        });
                    }
                    result.Add(new AnthropicMessage
                    {
                        Role = MessageRole.Assistant,
                        Content = blocks
                    });
                    continue;
                }

                if (msg.ContentParts != null && msg.ContentParts.Count > 0)
                {
                    var blocks = new List<ContentBlock>();
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        blocks.Add(new ContentBlock { Type = "text", Text = msg.Content });
                    }
                    foreach (var cp in msg.ContentParts)
                    {
                        if (cp.Type == ContentType.Text)
                        {
                            blocks.Add(new ContentBlock { Type = "text", Text = cp.Text });
                        }
                        else if (cp.Type == ContentType.Image)
                        {
                            if (!string.IsNullOrEmpty(cp.ImageUrl))
                            {
                                blocks.Add(new ContentBlock
                                {
                                    Type = "image",
                                    Source = cp.ImageUrl,
                                    MediaType = !string.IsNullOrEmpty(cp.MediaType) ? cp.MediaType : "image/png"
                                });
                            }
                            else if (!string.IsNullOrEmpty(cp.ImageBase64))
                            {
                                blocks.Add(new ContentBlock
                                {
                                    Type = "image",
                                    Source = cp.ImageBase64,
                                    MediaType = !string.IsNullOrEmpty(cp.MediaType) ? cp.MediaType : "image/png"
                                });
                            }
                        }
                    }
                    result.Add(new AnthropicMessage
                    {
                        Role = role,
                        Content = blocks
                    });
                }
                else
                {
                    result.Add(new AnthropicMessage
                    {
                        Role = role,
                        Content = msg.Content
                    });
                }
            }

            return result;
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
    }
}