using System;
using System.Collections.Generic;
using System.IO;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 工具审批回调委托。返回 true 表示批准执行，false 表示拒绝执行。
    /// </summary>
    /// <param name="args">工具调用事件参数（含函数名、参数、是否需要审批等信息）</param>
    /// <returns>是否批准执行该工具</returns>
    public delegate bool ToolApprovalCallback(ToolCallEventArgs args);

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
        private SkillManager _skillManager;
        private string _baseInstructions;
        private readonly ILogger _logger;

        // SystemPrompt 缓存，避免 AgentLoop 每次迭代重复 I/O
        private string _cachedSystemPrompt;
        private DateTime _lastPromptBuildTime;

        private const int DefaultMaxIterations = 10;
        private const int DefaultMaxHistoryMessages = 100;

        /// <summary>
        /// 工具调用循环的最大迭代次数。超过此次数后以最后一次内容作为最终回复。
        /// 默认值为 10，可在构造后修改。
        /// </summary>
        public int MaxIterations { get; set; }

        /// <summary>
        /// 对话历史最大消息数。超过此数量后自动裁剪最早的消息。
        /// 默认值为 100，设为 0 或负数表示不限制。
        /// </summary>
        public int MaxHistoryMessages { get; set; }

        /// <summary>
        /// 工具审批回调。设置为 true 表示批准执行，false 表示拒绝执行。
        /// 设置后，所有标记了 RequiresApproval 的工具调用都会先通过此回调审批。
        /// </summary>
        public ToolApprovalCallback ToolApproval { get; set; }

        /// <summary>
        /// 创建 AIAgent 实例（基础构造，不含默认工具和 Skills）
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="tools">可用的工具函数列表</param>
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools)
            : this(client, model, instructions, tools, false, (string[])null)
        {
        }

        /// <summary>
        /// 创建 AIAgent 实例（完整构造），支持自动集成默认工具和 Skills。
        /// SkillManager 自动持有目录路径，支持运行时文件变更感知和重新扫描。
        /// </summary>
        /// <param name="client">AI 客户端（OpenAI 或 Anthropic）</param>
        /// <param name="model">模型名称</param>
        /// <param name="instructions">系统指令/提示词</param>
        /// <param name="tools">用户自定义工具函数列表</param>
        /// <param name="includeDefaultTools">是否自动包含 AgentTools.CreateDefaultTools() 的默认工具</param>
        /// <param name="skillsDirectories">Skills 目录路径数组（优先级从低到高），传入后自动发现并集成渐进式披露</param>
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools, bool includeDefaultTools, string[] skillsDirectories)
        {
            _client = client;
            _baseInstructions = instructions;
            _skillManager = new SkillManager(skillsDirectories ?? new string[0]);
            _logger = new ConsoleLogger();

            _options = new ConversationOptions
            {
                Model = model,
                SystemPrompt = BuildSystemPrompt()
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

            _functions.Add(_skillManager.CreateLoadSkillFunction());
            _functions.Add(_skillManager.CreateReadSkillTool());

            _functionMap = new Dictionary<string, AIFunction>();
            foreach (var f in _functions)
            {
                if (f != null && !string.IsNullOrEmpty(f.Name))
                {
                    _functionMap[f.Name] = f;
                }
            }

            _conversationHistory = new List<ConversationMessage>();

            MaxIterations = DefaultMaxIterations;
            MaxHistoryMessages = DefaultMaxHistoryMessages;

            _client.ConfigureTools(_functions);
        }

        /// <summary>
        /// 裁剪对话历史，保留最近的 N 条消息
        /// </summary>
        /// <param name="keepLastN">保留的消息数量</param>
        public void TrimHistory(int keepLastN)
        {
            if (keepLastN <= 0)
            {
                _conversationHistory.Clear();
                return;
            }
            if (_conversationHistory.Count > keepLastN)
            {
                int removeCount = _conversationHistory.Count - keepLastN;
                _conversationHistory.RemoveRange(0, removeCount);
                _logger.Log(string.Format("Trimmed conversation history: removed {0} messages, keeping {1}", removeCount, keepLastN), "DEBUG");
            }
        }

        /// <summary>
        /// 获取当前对话历史消息数量
        /// </summary>
        public int HistoryCount
        {
            get { return _conversationHistory.Count; }
        }

        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        /// <summary>
        /// 添加消息到历史，并自动裁剪超出限制的部分
        /// </summary>
        private void AddToHistory(ConversationMessage message)
        {
            _conversationHistory.Add(message);
            if (MaxHistoryMessages > 0 && _conversationHistory.Count > MaxHistoryMessages)
            {
                TrimHistory(MaxHistoryMessages);
            }
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
        /// 设置模型名称
        /// </summary>
        /// <param name="model">模型名称（如 "gpt-4o"、"claude-sonnet-4-20250514"）</param>
        public void SetModel(string model)
        {
            if (!string.IsNullOrEmpty(model))
            {
                _options.Model = model;
            }
        }

        /// <summary>
        /// 获取当前 SkillManager 实例，可用于运行时操作（AddDirectory、RemoveDirectory、Refresh 等）。
        /// 通过此属性直接操作 SkillManager，无需经过 AIAgent 包装方法。
        /// </summary>
        public SkillManager SkillManager
        {
            get { return _skillManager; }
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
            return AgentLoop(onToolCall, MaxIterations);
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
            return AgentLoop(onToolCall, MaxIterations);
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
            // 验证类型约束：T 必须有公共无参构造函数或者是值类型
            var type = typeof(T);
            if (type.IsClass && !type.IsAbstract)
            {
                var constructor = type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                if (constructor == null)
                {
                    return new ApiResponse<T>
                    {
                        Error = new ApiError { Message = "Type " + type.Name + " must have a public parameterless constructor for structured output." }
                    };
                }
            }

            var schemaName = type.Name;
            var jsonSchema = JsonSchemaGenerator.GenerateFromType(type, schemaName);

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
        /// 构建当前的 SystemPrompt，包含原始指令 + SkillManager 渐进式披露目录。
        /// 每次调用前自动检查 SkillManager 文件更新，确保新技能能被 LLM 发现。
        /// </summary>
        private string BuildSystemPrompt()
        {
            // 缓存有效期 2 秒，避免工具调用循环中每次迭代重复文件 I/O
            // SkillManager.EnsureFresh 已在 BuildProgressivePrompt 内部触发
            if (_cachedSystemPrompt != null &&
                (DateTime.UtcNow - _lastPromptBuildTime).TotalSeconds < 2)
            {
                return _cachedSystemPrompt;
            }

            var skillPrompt = _skillManager.BuildProgressivePrompt();
            if (!string.IsNullOrEmpty(skillPrompt))
            {
                _cachedSystemPrompt = _baseInstructions + "\n\n" + skillPrompt;
            }
            else
            {
                _cachedSystemPrompt = _baseInstructions;
            }
            _lastPromptBuildTime = DateTime.UtcNow;
            return _cachedSystemPrompt;
        }

        /// <summary>
        /// 工具调用内部循环。持续调用模型直到无工具调用或达到最大迭代次数
        /// </summary>
        private ApiResponse<string> AgentLoop(Action<ToolCallEventArgs> onToolCall, int remainingIterations)
        {
            if (remainingIterations <= 0)
            {
                if (_conversationHistory.Count > 0)
                {
                    var lastMsg = _conversationHistory[_conversationHistory.Count - 1];
                    return new ApiResponse<string>
                    {
                        Result = lastMsg.Content != null ? lastMsg.Content : ""
                    };
                }
                return new ApiResponse<string>
                {
                    Error = new ApiError("Agent loop exceeded max iterations with empty history.")
                };
            }

            _options.SystemPrompt = BuildSystemPrompt();
            var response = _client.SendConversation(_conversationHistory, _options);

            if (!response.IsSuccess)
            {
                return new ApiResponse<string> { Error = response.Error };
            }

            var result = response.Result;
            var assistantMsg = new ConversationMessage
            {
                Role = MessageRole.Assistant,
                Content = result.Content != null ? result.Content : "",
                ReasoningContent = result.ReasoningContent
            };

            bool hasToolCalls = result.ToolCalls != null && result.ToolCalls.Count > 0;

            if (hasToolCalls)
            {
                assistantMsg.ToolCalls = new List<ToolCallRequest>(result.ToolCalls);
            }

            AddToHistory(assistantMsg);

            if (!hasToolCalls)
            {
                var metadata = new ApiResponseMetadata();
                if (!string.IsNullOrEmpty(result.Model))
                {
                    metadata.Model = result.Model;
                }
                if (!string.IsNullOrEmpty(result.FinishReason))
                {
                    metadata.FinishReason = result.FinishReason;
                }
                return new ApiResponse<string>
                {
                    Result = result.Content != null ? result.Content : "",
                    Metadata = metadata
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
            StreamingLoop(onUpdate, onError, onToolCall, MaxIterations);
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
            StreamingLoop(onUpdate, onError, onToolCall, MaxIterations);
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
                // 与 AgentLoop 保持一致：返回最后内容，不抛错误
                if (_conversationHistory.Count > 0)
                {
                    var lastMsg = _conversationHistory[_conversationHistory.Count - 1];
                    if (!string.IsNullOrEmpty(lastMsg.Content))
                    {
                        onUpdate(lastMsg.Content);
                    }
                }
                _logger.Log("Agent loop exceeded maximum iterations, returning last content", "WARN");
                return;
            }

            string fullResponse = "";
            string fullReasoning = "";
            var collectedToolCalls = new List<ToolCallRequest>();
            bool hasError = false;

            _options.SystemPrompt = BuildSystemPrompt();
            _client.SendConversationStreaming(
                _conversationHistory,
                _options,
                new Action<ConversationResponse>(chunk =>
                {
                    if (!string.IsNullOrEmpty(chunk.ReasoningContent))
                    {
                        fullReasoning += chunk.ReasoningContent;
                    }

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
                Content = fullResponse,
                ReasoningContent = fullReasoning
            };

            if (hasToolCalls)
            {
                assistantMsg.ToolCalls = collectedToolCalls;
            }

            AddToHistory(assistantMsg);

            if (!hasToolCalls)
            {
                return;
            }

            foreach (var toolCall in collectedToolCalls)
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
                    bool needsApproval = function.RequiresApproval;
                    if (function.ApprovalPredicate != null)
                    {
                        needsApproval = function.ApprovalPredicate(functionName, functionArgs);
                    }

                    if (needsApproval && ToolApproval != null)
                    {
                        var approvalArgs = new ToolCallEventArgs
                        {
                            FunctionName = functionName,
                            FunctionArguments = functionArgs,
                            ToolCallId = toolCall.Id,
                            RequiresApproval = true
                        };

                        if (!ToolApproval(approvalArgs))
                        {
                            AddToHistory(new ConversationMessage
                            {
                                Role = MessageRole.Tool,
                                Name = functionName,
                                ToolCallId = toolCall.Id,
                                Content = "[REJECTED] User denied execution of tool: " + functionName
                            });

                            if (onToolCall != null)
                            {
                                approvalArgs.Result = "[REJECTED]";
                                approvalArgs.IsApproved = false;
                                onToolCall(approvalArgs);
                            }
                            continue;
                        }
                    }

                    var result = function.Execute(functionArgs);
                    AddToHistory(new ConversationMessage
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
                else
                {
                    // 工具未注册：添加错误结果，确保对话历史完整
                    AddToHistory(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
                        Name = functionName,
                        ToolCallId = toolCall.Id,
                        Content = "Error: Tool '" + functionName + "' not found."
                    });
                }
            }

            StreamingLoop(onUpdate, onError, onToolCall, remainingIterations - 1);
        }

        /// <summary>
        /// 执行工具调用列表中的所有工具，并将结果添加到对话历史。
        /// 如果工具标记了 RequiresApproval 且设置了 ToolApprovalCallback，会先回调审批。
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
                    bool needsApproval = function.RequiresApproval;
                    if (function.ApprovalPredicate != null)
                    {
                        needsApproval = function.ApprovalPredicate(functionName, functionArgs);
                    }

                    if (needsApproval && ToolApproval != null)
                    {
                        var approvalArgs = new ToolCallEventArgs
                        {
                            FunctionName = functionName,
                            FunctionArguments = functionArgs,
                            ToolCallId = toolCall.Id,
                            RequiresApproval = true
                        };

                        if (!ToolApproval(approvalArgs))
                        {
                            AddToHistory(new ConversationMessage
                            {
                                Role = MessageRole.Tool,
                                Name = functionName,
                                ToolCallId = toolCall.Id,
                                Content = "[REJECTED] User denied execution of tool: " + functionName
                            });

                            if (onToolCall != null)
                            {
                                approvalArgs.Result = "[REJECTED]";
                                approvalArgs.IsApproved = false;
                                onToolCall(approvalArgs);
                            }
                            continue;
                        }
                    }

                    var result = function.Execute(functionArgs);
                    AddToHistory(new ConversationMessage
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
                else
                {
                    AddToHistory(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
                        Name = functionName,
                        ToolCallId = toolCall.Id,
                        Content = "Error: Tool '" + functionName + "' not found."
                    });
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
        /// <param name="skillsDirectories">Skills 目录路径数组（可选），优先级从低到高</param>
        /// <param name="extraTools">额外的自定义工具（可选）</param>
        /// <returns>配置完成的 AIAgent 实例</returns>
        public static AIAgent CreateWithDefaults(
            IAIClient client,
            string model,
            string instructions,
            string[] skillsDirectories = null,
            IEnumerable<AIFunction> extraTools = null)
        {
            return new AIAgent(client, model, instructions, extraTools, true, skillsDirectories);
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
        /// 获取三层 Skills 目录的默认路径（全局 → 项目 → 本地）。
        /// 优先级：local > project > global（同级 skill 高优先级覆盖低优先级）。
        /// </summary>
        /// <param name="projectSkillsDir">项目级 Skills 目录路径（可选）</param>
        /// <returns>按优先级排列的目录路径数组</returns>
        public static string[] GetDefaultSkillPaths(string projectSkillsDir = null)
        {
            var paths = new List<string>();
            paths.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".agents"));
            if (!string.IsNullOrEmpty(projectSkillsDir))
            {
                paths.Add(projectSkillsDir);
            }
            paths.Add(Path.Combine(
                projectSkillsDir ?? ".", "skills.local"));
            return paths.ToArray();
        }

        /// <summary>
        /// 将用户消息添加到对话历史
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（可为 null）</param>
        private void AddUserMessage(string userMessage, List<MessageContent> contentParts)
        {
            AddToHistory(new ConversationMessage
            {
                Role = MessageRole.User,
                Content = userMessage,
                ContentParts = contentParts
            });
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
