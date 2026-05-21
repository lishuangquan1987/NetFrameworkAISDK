using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI 代理，封装工具调用循环逻辑。提供统一接口，支持 OpenAI 和 Anthropic 等多种后端。
    /// </summary>
    public class AIAgent
    {
        private readonly IAIClient _client;
        private readonly ConversationOptions _options;
        private readonly List<AIFunction> _functions;
        private readonly Dictionary<string, AIFunction> _functionMap;
        private readonly List<ConversationMessage> _conversationHistory;
        private readonly List<SkillInfo> _skills;
        private readonly string _skillsDirectory;

        private const int DefaultMaxIterations = 10;

        /// <summary>
        /// 创建 AIAgent 实例（基础构造，不含默认工具和 Skills）
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="tools">可用的工具函数列表</param>
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools)
            : this(client, model, instructions, tools, false, null)
        {
        }

        /// <summary>
        /// 创建 AIAgent 实例（完整构造），支持自动集成默认工具和 Skills
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="tools">用户自定义工具函数列表</param>
        /// <param name="includeDefaultTools">是否自动包含 AgentTools.CreateDefaultTools() 的默认工具</param>
        /// <param name="skillsDirectory">Skills 目录路径，传入后自动发现并集成渐进式披露</param>
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools, bool includeDefaultTools, string skillsDirectory)
        {
            _client = client;
            _skills = new List<SkillInfo>();
            _skillsDirectory = skillsDirectory;

            string fullInstructions = instructions;

            if (!string.IsNullOrEmpty(skillsDirectory))
            {
                var discoveredSkills = SkillManager.DiscoverSkills(skillsDirectory);
                if (discoveredSkills != null && discoveredSkills.Count > 0)
                {
                    _skills = discoveredSkills;
                    var skillPrompt = SkillManager.BuildProgressivePrompt(_skills);
                    if (!string.IsNullOrEmpty(skillPrompt))
                    {
                        fullInstructions = fullInstructions + "\n\n" + skillPrompt;
                    }
                }
            }

            _options = new ConversationOptions
            {
                Model = model,
                SystemPrompt = fullInstructions
            };

            _functions = new List<AIFunction>();
            if (includeDefaultTools)
            {
                var defaultTools = AgentTools.CreateDefaultTools();
                if (defaultTools != null)
                {
                    _functions.AddRange(defaultTools);
                }
            }
            if (tools != null)
            {
                _functions.AddRange(tools);
            }

            if (_skills.Count > 0)
            {
                _functions.Add(SkillManager.CreateLoadSkillFunction(_skills));
                _functions.Add(SkillManager.CreateReadSkillTool(_skills));
            }

            _functionMap = new Dictionary<string, AIFunction>();
            foreach (var f in _functions)
            {
                if (f != null && !string.IsNullOrEmpty(f.Name))
                {
                    _functionMap[f.Name] = f;
                }
            }

            _conversationHistory = new List<ConversationMessage>();

            _client.ConfigureTools(_functions);
        }

        /// <summary>
        /// 动态添加工具函数
        /// </summary>
        /// <param name="function">要添加的 AI 函数</param>
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

        /// <summary>
        /// 设置温度参数（控制回复的随机性）
        /// </summary>
        /// <param name="temperature">温度值（0-2），null 使用默认值</param>
        public void SetTemperature(double? temperature)
        {
            _options.Temperature = temperature;
        }

        /// <summary>
        /// 设置最大 Token 数
        /// </summary>
        /// <param name="maxTokens">最大 Token 数，null 使用默认值</param>
        public void SetMaxTokens(int? maxTokens)
        {
            _options.MaxTokens = maxTokens;
        }

        /// <summary>
        /// 执行一次非流式对话，自动处理工具调用循环
        /// </summary>
        /// <param name="userMessage">用户输入消息</param>
        /// <param name="onToolCall">
        /// 工具调用回调（可选）。参数 <see cref="ToolCallEventArgs"/> 包含：
        /// FunctionName（函数名）、FunctionArguments（参数 JSON）、
        /// Result（执行结果）、ToolCallId（调用 ID）
        /// </param>
        /// <returns>包含最终 AI 回复或错误信息的响应</returns>
        public ApiResponse<string> Run(string userMessage, Action<ToolCallEventArgs> onToolCall = null)
        {
            AddUserMessage(userMessage, null);
            return AgentLoop(onToolCall, DefaultMaxIterations);
        }

        /// <summary>
        /// 执行一次非流式多模态对话，支持文本+图片输入
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（图片等），可为 null</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <returns>包含最终 AI 回复或错误信息的响应</returns>
        public ApiResponse<string> Run(string userMessage, List<MessageContent> contentParts, Action<ToolCallEventArgs> onToolCall = null)
        {
            AddUserMessage(userMessage, contentParts);
            return AgentLoop(onToolCall, DefaultMaxIterations);
        }

        /// <summary>
        /// 执行结构化对话，AI 输出强类型对象
        /// </summary>
        /// <typeparam name="T">期望的输出类型（需有公开无参构造函数）</typeparam>
        /// <param name="userMessage">用户消息</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <returns>包含反序列化对象或错误信息的响应</returns>
        public ApiResponse<T> RunStructured<T>(string userMessage, Action<ToolCallEventArgs> onToolCall = null)
        {
            var schemaName = typeof(T).Name;
            var jsonSchema = JsonSchemaGenerator.GenerateFromType(typeof(T), schemaName);

            _options.ResponseFormat = new ResponseFormat
            {
                Type = "json_schema",
                JsonSchema = jsonSchema,
                SchemaName = schemaName,
                Strict = true
            };

            var response = Run(userMessage, onToolCall);

            _options.ResponseFormat = null;

            if (!response.IsSuccess)
            {
                return new ApiResponse<T> { Error = response.Error };
            }

            try
            {
                var result = JsonHelper.Deserialize<T>(response.Result);
                return new ApiResponse<T> { Result = result };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T>
                {
                    Error = new ApiError { Message = "Structured output parse failed: " + ex.Message }
                };
            }
        }

        /// <summary>
        /// 工具调用内部循环。持续调用模型直到无工具调用或达到最大迭代次数
        /// </summary>
        private ApiResponse<string> AgentLoop(Action<ToolCallEventArgs> onToolCall, int remainingIterations)
        {
            if (remainingIterations <= 0)
            {
                var lastMsg = _conversationHistory[_conversationHistory.Count - 1];
                return new ApiResponse<string>
                {
                    Result = lastMsg.Content != null ? lastMsg.Content : ""
                };
            }

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

            return AgentLoop(onToolCall, remainingIterations - 1);
        }

        /// <summary>
        /// 执行流式对话，通过回调逐字输出 AI 回复，自动处理工具调用循环
        /// </summary>
        /// <param name="userMessage">用户输入消息</param>
        /// <param name="onUpdate">每收到一个文本块时回调（增量文本）</param>
        /// <param name="onError">发生错误时回调</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        public void RunStreaming(
            string userMessage,
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall = null)
        {
            AddUserMessage(userMessage, null);
            StreamingLoop(onUpdate, onError, onToolCall, DefaultMaxIterations);
        }

        /// <summary>
        /// 执行流式多模态对话，支持文本+图片输入
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（图片等），可为 null</param>
        /// <param name="onUpdate">每收到一个文本块时回调（增量文本）</param>
        /// <param name="onError">发生错误时回调</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        public void RunStreaming(
            string userMessage,
            List<MessageContent> contentParts,
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall = null)
        {
            AddUserMessage(userMessage, contentParts);
            StreamingLoop(onUpdate, onError, onToolCall, DefaultMaxIterations);
        }

        /// <summary>
        /// 流式工具调用循环。通过 SendConversationStreaming 收集分块数据，
        /// 合并工具调用后在内容块结束时触发
        /// </summary>
        private void StreamingLoop(
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall,
            int remainingIterations)
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
                        onToolCall(new ToolCallEventArgs
                        {
                            FunctionName = functionName,
                            FunctionArguments = functionArgs,
                            Result = result,
                            ToolCallId = toolCall.Id
                        });
                    }
                }
            }

            StreamingLoop(onUpdate, onError, onToolCall, remainingIterations - 1);
        }

        /// <summary>
        /// 执行工具调用列表中的所有工具，并将结果添加到对话历史
        /// </summary>
        private void ExecuteToolCalls(List<ToolCallRequest> toolCalls, Action<ToolCallEventArgs> onToolCall)
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
                        onToolCall(new ToolCallEventArgs
                        {
                            FunctionName = functionName,
                            FunctionArguments = functionArgs,
                            Result = result,
                            ToolCallId = toolCall.Id
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 合并流式响应中的增量工具调用数据（相同 Id 的工具调用累积参数）
        /// </summary>
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

        /// <summary>
        /// 便捷创建带默认工具和 Skills 的 AIAgent
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="skillsDirectory">Skills 目录路径（可选）</param>
        /// <param name="extraTools">额外的自定义工具（可选）</param>
        /// <returns>配置完成的 AIAgent 实例</returns>
        public static AIAgent CreateWithDefaults(
            IAIClient client,
            string model,
            string instructions,
            string skillsDirectory = null,
            IEnumerable<AIFunction> extraTools = null)
        {
            return new AIAgent(client, model, instructions, extraTools, true, skillsDirectory);
        }

        /// <summary>
        /// 便捷创建最小化 AIAgent（不含默认工具和 Skills）
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="tools">可用的工具函数列表（可选）</param>
        /// <returns>配置完成的 AIAgent 实例</returns>
        public static AIAgent CreateMinimal(
            IAIClient client,
            string model,
            string instructions,
            IEnumerable<AIFunction> tools = null)
        {
            return new AIAgent(client, model, instructions, tools, false, null);
        }

        /// <summary>
        /// 将用户消息添加到对话历史
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（可为 null）</param>
        private void AddUserMessage(string userMessage, List<MessageContent> contentParts)
        {
            _conversationHistory.Add(new ConversationMessage
            {
                Role = MessageRole.User,
                Content = userMessage,
                ContentParts = contentParts
            });
        }

        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        /// <summary>
        /// 获取当前对话历史的副本
        /// </summary>
        /// <returns>对话消息列表</returns>
        public List<ConversationMessage> GetHistory()
        {
            return new List<ConversationMessage>(_conversationHistory);
        }
    }
}