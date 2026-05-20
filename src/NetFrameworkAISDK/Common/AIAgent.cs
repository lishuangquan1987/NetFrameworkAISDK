using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    public class AIAgent
    {
        private readonly IAIClient _client;
        private readonly ConversationOptions _options;
        private readonly List<AIFunction> _functions;
        private readonly Dictionary<string, AIFunction> _functionMap;
        private readonly List<ConversationMessage> _conversationHistory;

        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools)
        {
            _client = client;
            _options = new ConversationOptions
            {
                Model = model,
                SystemPrompt = instructions
            };
            _functions = tools != null ? new List<AIFunction>(tools) : new List<AIFunction>();
            _functionMap = new Dictionary<string, AIFunction>();
            if (tools != null)
            {
                foreach (var f in tools)
                {
                    if (f != null && !string.IsNullOrEmpty(f.Name))
                    {
                        _functionMap[f.Name] = f;
                    }
                }
            }
            _conversationHistory = new List<ConversationMessage>();

            _client.ConfigureTools(_functions);
        }

        public void AddTool(AIFunction function)
        {
            if (function == null || string.IsNullOrEmpty(function.Name))
            {
                return;
            }
            _functions.Add(function);
            _functionMap[function.Name] = function;
            _client.ConfigureTools(_functions);
        }

        public void SetTemperature(double? temperature)
        {
            _options.Temperature = temperature;
        }

        public void SetMaxTokens(int? maxTokens)
        {
            _options.MaxTokens = maxTokens;
        }

        public ApiResponse<string> Run(string userMessage, Action<string, string, string> onToolCall = null)
        {
            _conversationHistory.Add(new ConversationMessage
            {
                Role = MessageRole.User,
                Content = userMessage
            });

            return AgentLoop(onToolCall);
        }

        private ApiResponse<string> AgentLoop(Action<string, string, string> onToolCall)
        {
            int maxIterations = 10;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                var response = _client.SendConversation(_conversationHistory, _options);

                if (!response.IsSuccess)
                {
                    return new ApiResponse<string> { Error = response.Error };
                }

                var result = response.Result;
                var assistantMsg = new ConversationMessage
                {
                    Role = MessageRole.Assistant,
                    Content = result.Content != null ? result.Content : ""
                };

                bool hasToolCalls = result.ToolCalls != null && result.ToolCalls.Count > 0;

                if (hasToolCalls)
                {
                    assistantMsg.ToolCalls = new List<ToolCallRequest>(result.ToolCalls);
                }

                _conversationHistory.Add(assistantMsg);

                if (!hasToolCalls)
                {
                    return new ApiResponse<string>
                    {
                        Result = result.Content != null ? result.Content : ""
                    };
                }

                ExecuteToolCalls(result.ToolCalls, onToolCall);
            }

            var lastMsg = _conversationHistory[_conversationHistory.Count - 1];
            return new ApiResponse<string>
            {
                Result = lastMsg.Content != null ? lastMsg.Content : ""
            };
        }

        public void RunStreaming(string userMessage, Action<string> onUpdate, Action<ApiError> onError, Action<string, string, string> onToolCall = null)
        {
            _conversationHistory.Add(new ConversationMessage
            {
                Role = MessageRole.User,
                Content = userMessage
            });

            StreamingLoop(onUpdate, onError, onToolCall, 10);
        }

        private void StreamingLoop(Action<string> onUpdate, Action<ApiError> onError, Action<string, string, string> onToolCall, int remainingIterations)
        {
            if (remainingIterations <= 0)
            {
                return;
            }

            string fullResponse = "";
            var collectedToolCalls = new List<ToolCallRequest>();
            bool hasError = false;

            _client.SendConversationStreaming(
                _conversationHistory,
                _options,
                new Action<ConversationResponse>(chunk =>
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        fullResponse += chunk.Content;
                        onUpdate(chunk.Content);
                    }

                    if (chunk.ToolCalls != null && chunk.ToolCalls.Count > 0)
                    {
                        foreach (var tc in chunk.ToolCalls)
                        {
                            MergeToolCall(collectedToolCalls, tc);
                        }
                    }
                }),
                new Action<ApiError>(error =>
                {
                    hasError = true;
                    onError(error);
                })
            );

            if (hasError)
            {
                return;
            }

            bool hasToolCalls = collectedToolCalls.Count > 0;

            var assistantMsg = new ConversationMessage
            {
                Role = MessageRole.Assistant,
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
                string functionName = toolCall.FunctionName;
                string functionArgs = toolCall.FunctionArguments != null ? toolCall.FunctionArguments : "{}";

                AIFunction function = null;
                if (_functionMap.ContainsKey(functionName))
                {
                    function = _functionMap[functionName];
                }

                if (function != null)
                {
                    var result = function.Execute(functionArgs);
                    _conversationHistory.Add(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
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

            StreamingLoop(onUpdate, onError, onToolCall, remainingIterations - 1);
        }

        private void ExecuteToolCalls(List<ToolCallRequest> toolCalls, Action<string, string, string> onToolCall)
        {
            foreach (var toolCall in toolCalls)
            {
                string functionName = toolCall.FunctionName;
                string functionArgs = toolCall.FunctionArguments != null ? toolCall.FunctionArguments : "{}";

                AIFunction function = null;
                if (!string.IsNullOrEmpty(functionName) && _functionMap.ContainsKey(functionName))
                {
                    function = _functionMap[functionName];
                }

                if (function != null)
                {
                    var result = function.Execute(functionArgs);
                    _conversationHistory.Add(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
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

        private static void MergeToolCall(List<ToolCallRequest> collected, ToolCallRequest delta)
        {
            foreach (var existing in collected)
            {
                if (existing.Id == delta.Id)
                {
                    if (delta.FunctionName != null)
                    {
                        existing.FunctionName = delta.FunctionName;
                    }
                    if (delta.FunctionArguments != null)
                    {
                        existing.FunctionArguments = (existing.FunctionArguments != null ? existing.FunctionArguments : "") + delta.FunctionArguments;
                    }
                    return;
                }
            }
            collected.Add(delta);
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        public List<ConversationMessage> GetHistory()
        {
            return new List<ConversationMessage>(_conversationHistory);
        }
    }
}