using NetFrameworkAI.Common;
using System;
using System.Collections.Generic;

namespace NetFrameworkAI.OpenAI
{
    public class AIAgent
    {
        private readonly OpenAIClient _client;
        private readonly string _model;
        private readonly string _systemInstructions;
        private readonly List<AIFunction> _functions;
        private readonly List<ChatMessage> _conversationHistory;

        public AIAgent(OpenAIClient client, string model, string instructions, IEnumerable<AIFunction> tools)
        {
            _client = client;
            _model = model;
            _systemInstructions = instructions;
            _functions = tools != null ? new List<AIFunction>(tools) : new List<AIFunction>();
            _conversationHistory = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(_systemInstructions))
            {
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = _systemInstructions
                });
            }
        }

        public void AddTool(AIFunction function)
        {
            _functions.Add(function);
        }

        public ApiResponse<string> Run(string userMessage, Action<string, string, string> onToolCall = null)
        {
            _conversationHistory.Add(new ChatMessage
            {
                Role = ChatRole.User,
                Content = userMessage
            });

            var toolDefs = new List<ToolDefinition>();
            foreach (var f in _functions)
            {
                toolDefs.Add(f.ToToolDefinition());
            }

            return AgentLoop(toolDefs, onToolCall);
        }

        private ApiResponse<string> AgentLoop(List<ToolDefinition> toolDefs, Action<string, string, string> onToolCall)
        {
            int maxIterations = 10;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                var response = _client.CreateChatCompletion(
                    _model,
                    _conversationHistory,
                    tools: toolDefs.Count > 0 ? toolDefs : null
                );

                if (!response.IsSuccess)
                {
                    return new ApiResponse<string> { Error = response.Error };
                }

                var choice = response.Result.Choices[0];
                var assistantMessage = choice.Message;
                _conversationHistory.Add(assistantMessage);

                bool hasToolCalls = assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0;

                if (!hasToolCalls)
                {
                    return new ApiResponse<string>
                    {
                        Result = assistantMessage.Content != null ? assistantMessage.Content : ""
                    };
                }

                ExecuteToolCalls(assistantMessage, onToolCall);
            }

            return new ApiResponse<string>
            {
                Result = _conversationHistory[_conversationHistory.Count - 1].Content != null
                    ? _conversationHistory[_conversationHistory.Count - 1].Content : ""
            };
        }

        public void RunStreaming(string userMessage, Action<string> onUpdate, Action<ApiError> onError, Action<string, string, string> onToolCall = null)
        {
            _conversationHistory.Add(new ChatMessage
            {
                Role = ChatRole.User,
                Content = userMessage
            });

            var toolDefs = new List<ToolDefinition>();
            foreach (var f in _functions)
            {
                toolDefs.Add(f.ToToolDefinition());
            }

            StreamingLoop(onUpdate, onError, onToolCall, toolDefs, 10);
        }

        private void StreamingLoop(Action<string> onUpdate, Action<ApiError> onError, Action<string, string, string> onToolCall, List<ToolDefinition> toolDefs, int remainingIterations)
        {
            if (remainingIterations <= 0)
            {
                return;
            }

            string fullResponse = "";
            var collectedToolCalls = new List<ToolCall>();

            _client.CreateChatCompletionStream(
                _model,
                _conversationHistory,
                new Action<ChatCompletionStreamResponse>(streamResponse =>
                {
                    if (streamResponse.Choices != null && streamResponse.Choices.Count > 0)
                    {
                        var delta = streamResponse.Choices[0].Delta;
                        if (delta != null)
                        {
                            if (!string.IsNullOrEmpty(delta.Content))
                            {
                                fullResponse += delta.Content;
                                onUpdate(delta.Content);
                            }

                            if (delta.ToolCalls != null && delta.ToolCalls.Count > 0)
                            {
                                foreach (var tc in delta.ToolCalls)
                                {
                                    MergeToolCall(collectedToolCalls, tc);
                                }
                            }
                        }
                    }
                }),
                onError
            );

            bool hasToolCalls = collectedToolCalls.Count > 0;

            var assistantMsg = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = fullResponse
            };

            if (hasToolCalls)
            {
                assistantMsg.ToolCalls = collectedToolCalls;
            }

            _conversationHistory.Add(assistantMsg);

            if (!hasToolCalls)
            {
                return;
            }

            foreach (var toolCall in collectedToolCalls)
            {
                if (toolCall.Function != null)
                {
                    string functionName = toolCall.Function.Name;
                    string functionArgs = toolCall.Function.Arguments != null ? toolCall.Function.Arguments : "{}";

                    AIFunction function = null;
                    foreach (var f in _functions)
                    {
                        if (f.Name == functionName)
                        {
                            function = f;
                            break;
                        }
                    }

                    if (function != null)
                    {
                        var result = function.Execute(functionArgs);
                        _conversationHistory.Add(new ChatMessage
                        {
                            Role = ChatRole.Tool,
                            Name = functionName,
                            ToolCallId = toolCall.Id,
                            Content = result
                        });

                        if (onToolCall != null)
                        {
                            onToolCall(functionName, functionArgs, result);
                        }
                    }
                }
            }

            StreamingLoop(onUpdate, onError, onToolCall, toolDefs, remainingIterations - 1);
        }

        private static void MergeToolCall(List<ToolCall> collected, ToolCall delta)
        {
            foreach (var existing in collected)
            {
                if (existing.Id == delta.Id)
                {
                    if (delta.Function != null)
                    {
                        if (existing.Function == null)
                        {
                            existing.Function = new FunctionCall();
                        }
                        if (delta.Function.Name != null)
                        {
                            existing.Function.Name = delta.Function.Name;
                        }
                        if (delta.Function.Arguments != null)
                        {
                            existing.Function.Arguments = (existing.Function.Arguments != null ? existing.Function.Arguments : "") + delta.Function.Arguments;
                        }
                    }
                    return;
                }
            }
            collected.Add(delta);
        }

        private void ExecuteToolCalls(ChatMessage assistantMessage, Action<string, string, string> onToolCall)
        {
            if (assistantMessage.ToolCalls == null)
            {
                return;
            }

            foreach (var toolCall in assistantMessage.ToolCalls)
            {
                string functionName = null;
                string functionArgs = "{}";
                if (toolCall.Function != null)
                {
                    functionName = toolCall.Function.Name;
                    functionArgs = toolCall.Function.Arguments != null ? toolCall.Function.Arguments : "{}";
                }

                AIFunction function = null;
                foreach (var f in _functions)
                {
                    if (f.Name == functionName)
                    {
                        function = f;
                        break;
                    }
                }

                if (function != null)
                {
                    var result = function.Execute(functionArgs);
                    _conversationHistory.Add(new ChatMessage
                    {
                        Role = ChatRole.Tool,
                        Name = functionName,
                        ToolCallId = toolCall.Id,
                        Content = result
                    });

                    if (onToolCall != null)
                    {
                        onToolCall(functionName, functionArgs, result);
                    }
                }
            }
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
            if (!string.IsNullOrEmpty(_systemInstructions))
            {
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = _systemInstructions
                });
            }
        }
    }
}