using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic API 客户端，封装 Messages API 调用。
    /// 支持文本生成、流式输出和工具调用，自动转换语义消息格式。
    /// </summary>
    public class AnthropicClient : AIClientBase
    {
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
        private const string ApiVersion = "2023-06-01";

        /// <summary>
        /// 创建 Anthropic 客户端（使用默认基础 URL）
        /// </summary>
        /// <param name="apiKey">Anthropic API 密钥</param>
        public AnthropicClient(string apiKey)
            : this(apiKey, DefaultBaseUrl)
        {
        }

        /// <summary>
        /// 创建 Anthropic 客户端
        /// </summary>
        /// <param name="apiKey">Anthropic API 密钥</param>
        /// <param name="baseUrl">自定义 API 基础 URL</param>
        public AnthropicClient(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
        }

        /// <inheritdoc />
        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["anthropic-version"] = ApiVersion;
            request.Headers["x-api-key"] = ApiKey;
        }

        /// <summary>
        /// 创建消息（非流式），发送后等待完整响应
        /// </summary>
        /// <param name="model">模型名称（如 "claude-sonnet-4-20250514"）</param>
        /// <param name="messages">消息列表</param>
        /// <param name="maxTokens">最大生成 token 数</param>
        /// <param name="system">系统提示（可选）</param>
        /// <param name="temperature">温度参数 0-1（可选）</param>
        /// <param name="tools">工具定义列表（可选）</param>
        /// <returns>包含消息响应或错误的 ApiResponse</returns>
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
        /// 创建消息（流式），通过 SSE 实时接收增量响应
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="maxTokens">最大生成 token 数</param>
        /// <param name="onEvent">收到 SSE 事件时的回调</param>
        /// <param name="onError">发生错误时的回调</param>
        /// <param name="system">系统提示（可选）</param>
        /// <param name="temperature">温度参数 0-1（可选）</param>
        /// <param name="tools">工具定义列表（可选）</param>
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

        /// <inheritdoc />
        public override ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options)
        {
            var anthropicMessages = ConvertToAnthropicMessages(messages);
            var toolDefs = BuildToolDefinitions(options);
            int maxTokens = options.MaxTokens.HasValue ? options.MaxTokens.Value : 1024;

            if (options.ResponseFormat != null)
            {
                if (toolDefs == null)
                {
                    toolDefs = new List<ToolDefinition>();
                }
                var structuredTool = BuildStructuredOutputTool(options.ResponseFormat);
                if (structuredTool != null)
                {
                    toolDefs.Add(structuredTool);
                }
            }

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

        /// <inheritdoc />
        public override void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError)
        {
            var anthropicMessages = ConvertToAnthropicMessages(messages);
            var toolDefs = BuildToolDefinitions(options);
            int maxTokens = options.MaxTokens.HasValue ? options.MaxTokens.Value : 1024;

            if (options.ResponseFormat != null)
            {
                if (toolDefs == null)
                {
                    toolDefs = new List<ToolDefinition>();
                }
                var structuredTool = BuildStructuredOutputTool(options.ResponseFormat);
                if (structuredTool != null)
                {
                    toolDefs.Add(structuredTool);
                }
            }

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
                                convResp.Content = null;
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

        /// <summary>
        /// 内容块流式状态，用于累积增量数据
        /// </summary>
        private class ContentBlockState
        {
            public string Type;
            public string Id;
            public string Name;
            public readonly System.Text.StringBuilder TextBuilder = new System.Text.StringBuilder();
        }

        /// <summary>
        /// 将语义层消息转换为 Anthropic 原生消息格式
        /// </summary>
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
                            var source = new ImageSource();
                            if (!string.IsNullOrEmpty(cp.ImageUrl))
                            {
                                source.Type = "url";
                                source.Data = cp.ImageUrl;
                                source.MediaType = !string.IsNullOrEmpty(cp.MediaType) ? cp.MediaType : "image/png";
                            }
                            else if (!string.IsNullOrEmpty(cp.ImageBase64))
                            {
                                source.Type = "base64";
                                source.Data = cp.ImageBase64;
                                source.MediaType = !string.IsNullOrEmpty(cp.MediaType) ? cp.MediaType : "image/png";
                            }
                            blocks.Add(new ContentBlock
                            {
                                Type = "image",
                                Source = source
                            });
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

        /// <summary>
        /// 将 Anthropic 原生响应转换为语义层响应
        /// </summary>
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
                        if (block.Name == "structured_output")
                        {
                            result.Content = block.Input != null ? JsonHelper.Serialize(block.Input) : "{}";
                            result.FinishReason = "stop";
                        }
                        else
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
            }

            return result;
        }

        /// <summary>
        /// 从 ResponseFormat 构建结构化输出工具（Anthropic tool-use hack）
        /// </summary>
        private ToolDefinition BuildStructuredOutputTool(ResponseFormat format)
        {
            if (format == null || string.IsNullOrEmpty(format.JsonSchema))
            {
                return null;
            }

            var schemaObj = JsonHelper.Deserialize<Dictionary<string, object>>(format.JsonSchema);
            var toolDef = new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "structured_output",
                    Description = "Output the structured data as a valid JSON object matching the required schema",
                    Parameters = schemaObj
                }
            };

            return toolDef;
        }
    }
}