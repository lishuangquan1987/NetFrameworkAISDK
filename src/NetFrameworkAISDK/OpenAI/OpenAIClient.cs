using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI API 客户端，封装 Chat Completions API 调用。
    /// 支持文本生成、流式输出和工具调用，自动转换语义消息格式。
    /// </summary>
    public class OpenAIClient : AIClientBase
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1";

        /// <summary>
        /// 创建 OpenAI 客户端（使用默认基础 URL）
        /// </summary>
        /// <param name="apiKey">OpenAI API 密钥</param>
        public OpenAIClient(string apiKey)
            : this(apiKey, DefaultBaseUrl)
        {
        }

        /// <summary>
        /// 创建 OpenAI 客户端
        /// </summary>
        /// <param name="apiKey">OpenAI API 密钥</param>
        /// <param name="baseUrl">自定义 API 基础 URL</param>
        public OpenAIClient(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
        }

        /// <inheritdoc />
        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["Authorization"] = "Bearer " + ApiKey;
        }

        /// <summary>
        /// 创建聊天完成（非流式），发送后等待完整响应
        /// </summary>
        /// <param name="model">模型名称（如 "gpt-4o"）</param>
        /// <param name="messages">消息列表</param>
        /// <param name="temperature">温度参数 0-2（可选）</param>
        /// <param name="maxTokens">最大生成 token 数（可选）</param>
        /// <param name="tools">工具定义列表（可选）</param>
        /// <returns>包含聊天完成响应或错误的 ApiResponse</returns>
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
        /// 创建聊天完成（流式），通过 SSE 实时接收增量响应
        /// </summary>
        /// <param name="model">模型名称</param>
        /// <param name="messages">消息列表</param>
        /// <param name="onData">收到流式分片时的回调</param>
        /// <param name="onError">发生错误时的回调</param>
        /// <param name="temperature">温度参数 0-2（可选）</param>
        /// <param name="maxTokens">最大生成 token 数（可选）</param>
        /// <param name="tools">工具定义列表（可选）</param>
        /// <param name="responseFormat">响应格式（可选，用于结构化输出）</param>
        public void CreateChatCompletionStream(
            string model,
            List<ChatMessage> messages,
            Action<ChatCompletionStreamResponse> onData,
            Action<ApiError> onError,
            double? temperature = null,
            int? maxTokens = null,
            List<ToolDefinition> tools = null,
            OpenAiResponseFormat responseFormat = null)
        {
            var request = new ChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Temperature = temperature,
                MaxTokens = maxTokens,
                Tools = tools,
                ResponseFormat = responseFormat,
                Stream = true
            };

            PostStream("chat/completions", request, onData, onError);
        }

        /// <inheritdoc />
        public override ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options)
        {
            var openAiMessages = ConvertToOpenAiMessages(messages, options);
            var toolDefs = BuildToolDefinitions(options);

            var request = new ChatCompletionRequest
            {
                Model = options.Model,
                Messages = openAiMessages,
                Temperature = options.Temperature,
                MaxTokens = options.MaxTokens,
                Tools = toolDefs,
                Stream = false
            };

            if (options.ResponseFormat != null)
            {
                var schemaObj = JsonHelper.Deserialize<object>(options.ResponseFormat.JsonSchema);
                request.ResponseFormat = new OpenAiResponseFormat
                {
                    Type = options.ResponseFormat.Type,
                    JsonSchema = new JsonSchemaObject
                    {
                        Name = options.ResponseFormat.SchemaName,
                        Strict = options.ResponseFormat.Strict,
                        Schema = schemaObj
                    }
                };
            }

            var response = Post<ChatCompletionResponse>("chat/completions", request);

            if (!response.IsSuccess)
            {
                return new ApiResponse<ConversationResponse> { Error = response.Error };
            }

            return new ApiResponse<ConversationResponse>
            {
                Result = ConvertFromOpenAiResponse(response.Result)
            };
        }

        /// <inheritdoc />
        public override void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError)
        {
            var openAiMessages = ConvertToOpenAiMessages(messages, options);
            var toolDefs = BuildToolDefinitions(options);

            OpenAiResponseFormat responseFormat = null;
            if (options.ResponseFormat != null)
            {
                var schemaObj = JsonHelper.Deserialize<object>(options.ResponseFormat.JsonSchema);
                responseFormat = new OpenAiResponseFormat
                {
                    Type = options.ResponseFormat.Type,
                    JsonSchema = new JsonSchemaObject
                    {
                        Name = options.ResponseFormat.SchemaName,
                        Strict = options.ResponseFormat.Strict,
                        Schema = schemaObj
                    }
                };
            }

            CreateChatCompletionStream(
                options.Model,
                openAiMessages,
                new Action<ChatCompletionStreamResponse>(streamResponse =>
                {
                    if (streamResponse.Choices != null && streamResponse.Choices.Count > 0)
                    {
                        var delta = streamResponse.Choices[0].Delta;
                        if (delta != null)
                        {
                            bool hasContent = !string.IsNullOrEmpty(delta.Content);
                            bool hasToolCalls = delta.ToolCalls != null && delta.ToolCalls.Count > 0;
                            bool hasReasoning = !string.IsNullOrEmpty(delta.ReasoningContent);

                            if (!hasContent && !hasToolCalls && !hasReasoning)
                            {
                                return;
                            }

                            var convResp = new ConversationResponse
                            {
                                Model = streamResponse.Model,
                                Content = delta.Content,
                                ReasoningContent = delta.ReasoningContent,
                                FinishReason = streamResponse.Choices[0].FinishReason
                            };

                            if (hasToolCalls)
                            {
                                convResp.ToolCalls = new List<ToolCallRequest>();
                                foreach (var tc in delta.ToolCalls)
                                {
                                    convResp.ToolCalls.Add(new ToolCallRequest
                                    {
                                        Id = tc.Id,
                                        Type = tc.Type,
                                        FunctionName = tc.Function != null ? tc.Function.Name : null,
                                        FunctionArguments = tc.Function != null ? tc.Function.Arguments : null
                                    });
                                }
                            }

                            onChunk(convResp);
                        }
                    }
                }),
                onError,
                options.Temperature,
                options.MaxTokens,
                toolDefs,
                responseFormat);
        }

        /// <summary>
        /// 将语义层消息转换为 OpenAI 原生消息格式
        /// </summary>
        private List<ChatMessage> ConvertToOpenAiMessages(List<ConversationMessage> messages, ConversationOptions options)
        {
            var result = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(options.SystemPrompt))
            {
                result.Add(new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = options.SystemPrompt
                });
            }

            foreach (var msg in messages)
            {
                var chatMsg = new ChatMessage
                {
                    Role = msg.Role,
                    Name = msg.Name ?? "",
                    ToolCallId = msg.ToolCallId
                };

                if (msg.ContentParts != null && msg.ContentParts.Count > 0)
                {
                    var parts = new List<ImageContentPart>();
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        parts.Add(new ImageContentPart
                        {
                            Type = "text",
                            Text = msg.Content
                        });
                    }
                    foreach (var cp in msg.ContentParts)
                    {
                        if (cp.Type == ContentType.Text)
                        {
                            parts.Add(new ImageContentPart
                            {
                                Type = "text",
                                Text = cp.Text
                            });
                        }
                        else if (cp.Type == ContentType.Image)
                        {
                            var imagePart = new ImageContentPart
                            {
                                Type = "image_url"
                            };

                            if (!string.IsNullOrEmpty(cp.ImageUrl))
                            {
                                imagePart.Image = new ImageDetail
                                {
                                    Url = cp.ImageUrl,
                                    Detail = cp.Detail
                                };
                            }
                            else if (!string.IsNullOrEmpty(cp.ImageBase64))
                            {
                                var mediaType = !string.IsNullOrEmpty(cp.MediaType) ? cp.MediaType : "image/png";
                                imagePart.Image = new ImageDetail
                                {
                                    Url = "data:" + mediaType + ";base64," + cp.ImageBase64,
                                    Detail = cp.Detail
                                };
                            }

                            parts.Add(imagePart);
                        }
                    }
                    chatMsg.ContentParts = parts;
                }
                else
                {
                    chatMsg.Content = msg.Content;
                }

                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    chatMsg.ToolCalls = new List<ToolCall>();
                    foreach (var tc in msg.ToolCalls)
                    {
                        chatMsg.ToolCalls.Add(new ToolCall
                        {
                            Id = tc.Id,
                            Type = tc.Type ?? "function",
                            Function = new FunctionCall
                            {
                                Name = tc.FunctionName,
                                Arguments = tc.FunctionArguments
                            }
                        });
                    }
                }

                // DeepSeek 思考模式：reasoning_content 必须原样传回
                if (!string.IsNullOrEmpty(msg.ReasoningContent))
                {
                    chatMsg.ReasoningContent = msg.ReasoningContent;
                }

                result.Add(chatMsg);
            }

            return result;
        }

        /// <summary>
        /// 将 OpenAI 原生响应转换为语义层响应
        /// </summary>
        private ConversationResponse ConvertFromOpenAiResponse(ChatCompletionResponse openAiResponse)
        {
            var result = new ConversationResponse
            {
                Model = openAiResponse.Model
            };

            if (openAiResponse.Choices != null && openAiResponse.Choices.Count > 0)
            {
                var choice = openAiResponse.Choices[0];
                result.FinishReason = choice.FinishReason;

                if (choice.Message != null)
                {
                    result.Content = choice.Message.Content;
                    result.ReasoningContent = choice.Message.ReasoningContent;

                    if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                    {
                        result.ToolCalls = new List<ToolCallRequest>();
                        foreach (var tc in choice.Message.ToolCalls)
                        {
                            result.ToolCalls.Add(new ToolCallRequest
                            {
                                Id = tc.Id,
                                Type = tc.Type,
                                FunctionName = tc.Function != null ? tc.Function.Name : null,
                                FunctionArguments = tc.Function != null ? tc.Function.Arguments : null
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}
