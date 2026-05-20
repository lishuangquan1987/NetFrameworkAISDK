using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI API client
    /// </summary>
    public class OpenAIClient : AIClientBase
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1";

        public OpenAIClient(string apiKey)
            : this(apiKey, DefaultBaseUrl)
        {
        }

        public OpenAIClient(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
        }

        protected override void ConfigureRequest(HttpWebRequest request)
        {
            request.Headers["Authorization"] = "Bearer " + ApiKey;
        }

        /// <summary>
        /// Create chat completion (non-streaming)
        /// </summary>
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
        /// Create chat completion (streaming)
        /// </summary>
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

        public override ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options)
        {
            var openAiMessages = ConvertToOpenAiMessages(messages, options);
            var toolDefs = BuildToolDefinitions(options);

            var response = CreateChatCompletion(
                options.Model,
                openAiMessages,
                options.Temperature,
                options.MaxTokens,
                toolDefs);

            if (!response.IsSuccess)
            {
                return new ApiResponse<ConversationResponse> { Error = response.Error };
            }

            return new ApiResponse<ConversationResponse>
            {
                Result = ConvertFromOpenAiResponse(response.Result)
            };
        }

        public override void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError)
        {
            var openAiMessages = ConvertToOpenAiMessages(messages, options);
            var toolDefs = BuildToolDefinitions(options);

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
                            var convResp = new ConversationResponse
                            {
                                Model = streamResponse.Model,
                                Content = delta.Content,
                                FinishReason = streamResponse.Choices[0].FinishReason
                            };

                            if (delta.ToolCalls != null && delta.ToolCalls.Count > 0)
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
                toolDefs);
        }

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
                    Name = msg.Name,
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
                            Image = new ImageDetail { Url = msg.Content }
                        });
                    }
                    foreach (var cp in msg.ContentParts)
                    {
                        if (cp.Type == ContentType.Text)
                        {
                            parts.Add(new ImageContentPart
                            {
                                Type = "text",
                                Image = new ImageDetail { Url = cp.Text }
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

                result.Add(chatMsg);
            }

            return result;
        }

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