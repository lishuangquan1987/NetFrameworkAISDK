using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

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
        private readonly object _historyLock = new object();

        private SkillManager _skillManager;
        private string _baseInstructions;
        private readonly ILogger _logger;

        // SystemPrompt 缓存，避免 AgentLoop 每次迭代重复 I/O
        private string _cachedSystemPrompt;
        private DateTime _lastPromptBuildTime;
        private string _structuredSchemaHint;

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
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools = null)
            : this(client, model, instructions, tools, false, (string[])null, null)
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
        public AIAgent(IAIClient client, string model, string instructions, IEnumerable<AIFunction> tools, bool includeDefaultTools, string[] skillsDirectories, ILogger logger = null)
        {
            _client = client;
            _baseInstructions = instructions;
            _skillManager = new SkillManager(skillsDirectories ?? new string[0]);
            _logger = logger ?? new FileLogger();

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
            lock (_historyLock)
            {
                _conversationHistory.Clear();
            }
        }

        /// <summary>
        /// 添加消息到历史，并自动裁剪超出限制的部分
        /// </summary>
        private void AddToHistory(ConversationMessage message)
        {
            lock (_historyLock)
            {
                _conversationHistory.Add(message);
                if (MaxHistoryMessages > 0 && _conversationHistory.Count > MaxHistoryMessages)
                {
                    int removeCount = _conversationHistory.Count - MaxHistoryMessages;
                    _conversationHistory.RemoveRange(0, removeCount);
                }
            }
        }

        /// <summary>
        /// 批量添加历史对话消息（恢复上下文、预填充多轮对话等）
        /// </summary>
        /// <param name="messages">要添加的 ConversationMessage 列表</param>
        public void AddHistorys(IEnumerable<ConversationMessage> messages)
        {
            if (messages == null) return;
            lock (_historyLock)
            {
                foreach (var msg in messages)
                {
                    if (msg != null)
                    {
                        _conversationHistory.Add(msg);
                    }
                }
                // 裁剪超出限制
                if (MaxHistoryMessages > 0 && _conversationHistory.Count > MaxHistoryMessages)
                {
                    int removeCount = _conversationHistory.Count - MaxHistoryMessages;
                    _conversationHistory.RemoveRange(0, removeCount);
                }
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
        /// 设置思考/推理模式开关。
        /// </summary>
        /// <param name="enable">true 开启，false 关闭，null 使用模型默认行为</param>
        public void SetEnableThinking(bool? enable)
        {
            _options.EnableThinking = enable;
        }

        /// <summary>
        /// 设置思考努力程度（仅 OpenAI 有效）。
        /// </summary>
        /// <param name="effort">可选值参见 <see cref="ThinkingEffort"/> 常量</param>
        public void SetThinkingEffort(string effort)
        {
            _options.ThinkingEffort = effort;
        }

        /// <summary>
        /// 设置思考预算 Token 数（仅 Anthropic 有效）。
        /// </summary>
        /// <param name="budgetTokens">预算 token 数，须 ≥ 1024；null 由 SDK 自动使用默认值</param>
        public void SetThinkingBudgetTokens(int? budgetTokens)
        {
            _options.ThinkingBudgetTokens = budgetTokens;
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
        /// <param name="onReasoning">思考/推理内容回调（可选，如 DeepSeek-R1 的 reasoning_content）</param>
        /// <param name="cancellationToken">取消令牌（可选），在工具调用循环中各迭代前检查</param>
        /// <returns>包含最终 AI 回复或错误信息的响应</returns>
        public ApiResponse<string> Run(string userMessage, Action<ToolCallEventArgs> onToolCall = null, Action<string> onReasoning = null, CancellationToken? cancellationToken = null)
        {
            AddUserMessage(userMessage, null);
            return AgentLoop(onToolCall, MaxIterations, onReasoning, cancellationToken);
        }

        /// <summary>
        /// 执行一次非流式多模态对话，支持文本+图片输入
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（图片等），可为 null</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <param name="onReasoning">思考/推理内容回调（可选，如 DeepSeek-R1 的 reasoning_content）</param>
        /// <param name="cancellationToken">取消令牌（可选），在工具调用循环中各迭代前检查</param>
        /// <returns>包含最终 AI 回复或错误信息的响应</returns>
        public ApiResponse<string> Run(string userMessage, List<MessageContent> contentParts, Action<ToolCallEventArgs> onToolCall = null, Action<string> onReasoning = null, CancellationToken? cancellationToken = null)
        {
            AddUserMessage(userMessage, contentParts);
            return AgentLoop(onToolCall, MaxIterations, onReasoning, cancellationToken);
        }

        /// <summary>
        /// 执行结构化对话，AI 输出强类型对象
        /// </summary>
        /// <typeparam name="T">期望的输出类型（需有公开无参构造函数）</typeparam>
        /// <param name="userMessage">用户消息</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <param name="onReasoning">思考/推理内容回调（可选）</param>
        /// <returns>包含反序列化对象或错误信息的响应</returns>
        public ApiResponse<T> RunStructured<T>(string userMessage, Action<ToolCallEventArgs> onToolCall = null, Action<string> onReasoning = null)
        {
            // 验证：不能是抽象类或接口（无法 new T()）
            var type = typeof(T);
            if (type.IsAbstract || type.IsInterface)
            {
                return new ApiResponse<T>
                {
                    Error = new ApiError { Message = "Type " + type.Name + " cannot be abstract or an interface for structured output." }
                };
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

            var response = Run(userMessage, onToolCall, onReasoning);

            // 如果 json_schema 不被支持（如 DeepSeek），回退到 json_object
            if (!response.IsSuccess && response.Error != null
                && response.Error.Message.IndexOf("response_format", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.Log("json_schema not supported, falling back to json_object", "DEBUG");

                _options.ResponseFormat = new ResponseFormat
                {
                    Type = "json_object"
                };

                // 把 JSON Schema 描述注入 system prompt
                _structuredSchemaHint = "You MUST output a valid JSON object matching this structure.\n"
                    + "DO NOT include explanations, only output the JSON.\n"
                    + "Required JSON schema:\n" + jsonSchema;

                response = Run(userMessage, onToolCall, onReasoning);
            }

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

            // 注入结构化输出的 Schema 提示（json_object 回退模式）
            if (!string.IsNullOrEmpty(_structuredSchemaHint))
            {
                _cachedSystemPrompt = _cachedSystemPrompt + "\n\n" + _structuredSchemaHint;
                _structuredSchemaHint = null;
            }

            _lastPromptBuildTime = DateTime.UtcNow;
            return _cachedSystemPrompt;
        }

        /// <summary>
        /// 工具调用内部循环。持续调用模型直到无工具调用或达到最大迭代次数
        /// </summary>
        private ApiResponse<string> AgentLoop(Action<ToolCallEventArgs> onToolCall, int remainingIterations, Action<string> onReasoning = null, CancellationToken? cancellationToken = null)
        {
            if (remainingIterations <= 0)
            {
                // 从后往前找最后一条 assistant 文本回复，避免返回工具执行结果
                for (int i = _conversationHistory.Count - 1; i >= 0; i--)
                {
                    var msg = _conversationHistory[i];
                    if (msg.Role == MessageRole.Assistant && !string.IsNullOrEmpty(msg.Content))
                    {
                        return new ApiResponse<string>
                        {
                            Result = msg.Content
                        };
                    }
                }
                return new ApiResponse<string>
                {
                    Error = new ApiError("Agent loop exceeded max iterations without a valid response.")
                };
            }

            _options.SystemPrompt = BuildSystemPrompt();

            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
            {
                return new ApiResponse<string> { Error = new ApiError("Request cancelled") };
            }

            ApiResponse<ConversationResponse> response;
            try
            {
                response = _client.SendConversation(_conversationHistory, _options, cancellationToken);

                if (!response.IsSuccess)
                {
                    return new ApiResponse<string> { Error = response.Error };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<string> { Error = new ApiError("Agent loop API call failed: " + ex.Message) };
            }

            var result = response.Result;
            var assistantMsg = new ConversationMessage
            {
                Role = MessageRole.Assistant,
                Content = result.Content != null ? result.Content : "",
                ReasoningContent = result.ReasoningContent
            };

            // 通知思考内容
            if (onReasoning != null && !string.IsNullOrEmpty(result.ReasoningContent))
            {
                onReasoning(result.ReasoningContent);
            }

            bool hasToolCalls = result.ToolCalls != null && result.ToolCalls.Count > 0;

            if (hasToolCalls)
            {
                assistantMsg.ToolCalls = new List<ToolCallRequest>(result.ToolCalls);
                // DeepSeek 思考模式要求 assistant 带 tool_calls 时有非空 name 字段
                if (string.IsNullOrEmpty(assistantMsg.Name))
                {
                    assistantMsg.Name = "assistant";
                }
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

            ExecuteToolCalls(result.ToolCalls, onToolCall, cancellationToken);

            return AgentLoop(onToolCall, remainingIterations - 1, onReasoning, cancellationToken);
        }

        /// <summary>
        /// 执行流式对话，通过回调逐字输出 AI 回复，自动处理工具调用循环
        /// </summary>
        /// <param name="userMessage">用户输入消息</param>
        /// <param name="onUpdate">每收到一个文本块时回调（增量文本）</param>
        /// <param name="onError">发生错误时回调</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <param name="onReasoning">思考/推理内容回调（可选，如 DeepSeek-R1 的 reasoning_content）</param>
        /// <param name="cancellationToken">取消令牌（可选），在工具调用循环中各迭代前检查</param>
        public void RunStreaming(
            string userMessage,
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall = null,
            Action<string> onReasoning = null,
            CancellationToken? cancellationToken = null)
        {
            AddUserMessage(userMessage, null);
            StreamingLoop(onUpdate, onError, onToolCall, MaxIterations, onReasoning, cancellationToken);
        }

        /// <summary>
        /// 执行流式多模态对话，支持文本+图片输入
        /// </summary>
        /// <param name="userMessage">用户文本消息</param>
        /// <param name="contentParts">多模态内容块列表（图片等），可为 null</param>
        /// <param name="onUpdate">每收到一个文本块时回调（增量文本）</param>
        /// <param name="onError">发生错误时回调</param>
        /// <param name="onToolCall">工具调用回调（可选）</param>
        /// <param name="onReasoning">思考/推理内容回调（可选，如 DeepSeek-R1 的 reasoning_content）</param>
        /// <param name="cancellationToken">取消令牌（可选），触发时中止请求</param>
        public void RunStreaming(
            string userMessage,
            List<MessageContent> contentParts,
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall = null,
            Action<string> onReasoning = null,
            CancellationToken? cancellationToken = null)
        {
            AddUserMessage(userMessage, contentParts);
            StreamingLoop(onUpdate, onError, onToolCall, MaxIterations, onReasoning, cancellationToken);
        }

        /// <summary>
        /// 流式工具调用循环。通过 SendConversationStreaming 收集分块数据，
        /// 合并工具调用后在内容块结束时触发
        /// </summary>
        private void StreamingLoop(
            Action<string> onUpdate,
            Action<ApiError> onError,
            Action<ToolCallEventArgs> onToolCall,
            int remainingIterations,
            Action<string> onReasoning = null,
            CancellationToken? cancellationToken = null)
        {
            if (remainingIterations <= 0)
            {
                // 从后往前找最后一条 assistant 文本回复，避免返回工具执行结果
                for (int i = _conversationHistory.Count - 1; i >= 0; i--)
                {
                    var msg = _conversationHistory[i];
                    if (msg.Role == MessageRole.Assistant && !string.IsNullOrEmpty(msg.Content))
                    {
                        onUpdate(msg.Content);
                        _logger.Log("Agent loop exceeded maximum iterations, returning last assistant content", "WARN");
                        return;
                    }
                }
                _logger.Log("Agent loop exceeded maximum iterations, no assistant content found", "WARN");
                return;
            }

            string fullResponse = "";
            string fullReasoning = "";
            var collectedToolCalls = new List<ToolCallRequest>();
            bool hasError = false;

            try
            {
                _options.SystemPrompt = BuildSystemPrompt();
            }
            catch (Exception ex)
            {
                _logger.Log("Failed to build system prompt: " + ex.Message, "ERROR");
                onError(new ApiError("System prompt build failed: " + ex.Message));
                return;
            }

            // 检查取消
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
            {
                onError(new ApiError("Request cancelled"));
                return;
            }

            try
            {
                _client.SendConversationStreaming(
                    _conversationHistory,
                    _options,
                    new Action<ConversationResponse>(chunk =>
                    {
                        if (!string.IsNullOrEmpty(chunk.ReasoningContent))
                        {
                            fullReasoning += chunk.ReasoningContent;
                            if (onReasoning != null) onReasoning(chunk.ReasoningContent);
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
                    }),
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                hasError = true;
                onError(new ApiError("Streaming API call failed: " + ex.Message));
            }

            if (hasError)
            {
                // 流式出错，回滚最后一条用户消息避免历史中出现孤立的 User 消息
                RemoveLastUserMessage();
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
                // DeepSeek 思考模式要求 assistant 带 tool_calls 时有非空 name 字段
                if (string.IsNullOrEmpty(assistantMsg.Name))
                {
                    assistantMsg.Name = "assistant";
                }
            }

            AddToHistory(assistantMsg);

            if (!hasToolCalls)
            {
                return;
            }

            try
            {
                ExecuteToolCalls(collectedToolCalls, onToolCall, cancellationToken);

                StreamingLoop(onUpdate, onError, onToolCall, remainingIterations - 1, onReasoning, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Log("StreamingLoop iteration failed: " + ex.Message, "ERROR");
                onError(new ApiError("StreamingLoop iteration failed: " + ex.Message));
            }
        }

        /// <summary>
        /// 执行工具调用列表中的所有工具，并将结果添加到对话历史。
        /// 如果工具标记了 RequiresApproval 且设置了 ToolApprovalCallback，会先回调审批。
        /// </summary>
        private void ExecuteToolCalls(List<ToolCallRequest> toolCalls, Action<ToolCallEventArgs> onToolCall, CancellationToken? cancellationToken = null)
        {
            foreach (var toolCall in toolCalls)
            {
                // 每次工具调用前检查取消
                if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
                {
                    AddToHistory(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
                        Name = !string.IsNullOrEmpty(toolCall.FunctionName) ? toolCall.FunctionName : "unknown",
                        ToolCallId = toolCall.Id,
                        Content = "[CANCELLED]"
                    });
                    continue;
                }
                string functionName = toolCall.FunctionName;
                string functionArgs = !string.IsNullOrEmpty(toolCall.FunctionArguments) ? toolCall.FunctionArguments : "{}";

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

                    string result;
                    try
                    {
                        result = function.Execute(functionArgs);
                    }
                    catch (Exception ex)
                    {
                        result = "[ERROR] Tool execution failed: " + ex.Message;
                    }
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
                    // 工具未注册或无函数名：添加错误结果
                    AddToHistory(new ConversationMessage
                    {
                        Role = MessageRole.Tool,
                        Name = !string.IsNullOrEmpty(functionName) ? functionName : "unknown",
                        ToolCallId = toolCall.Id,
                        Content = "Error: Tool '" + (functionName ?? "(null)") + "' not found."
                    });
                }
            }
        }

        /// <summary>
        /// 合并流式响应中的增量工具调用数据（相同 Id 的工具调用累积参数）
        /// </summary>
        private static void MergeToolCall(List<ToolCallRequest> collected, ToolCallRequest delta)
        {
            // 有 Id：按 Id 匹配已有条目（首个分片通常带有完整 Id）
            if (!string.IsNullOrEmpty(delta.Id))
            {
                foreach (var existing in collected)
                {
                    if (existing.Id == delta.Id)
                    {
                        MergeDelta(existing, delta);
                        return;
                    }
                }
                collected.Add(delta);
                return;
            }

            // 无 Id、有 Index：按 Index 匹配（后续参数分片通常只带 index 和 arguments）
            if (delta.Index.HasValue)
            {
                foreach (var existing in collected)
                {
                    if (existing.Index.HasValue && existing.Index.Value == delta.Index.Value)
                    {
                        MergeDelta(existing, delta);
                        return;
                    }
                }
                // 未找到匹配的 index 条目，但有参数数据，仍需保留
                if (delta.FunctionArguments != null || delta.FunctionName != null)
                {
                    collected.Add(delta);
                }
                return;
            }

            // 既无 Id 也无 Index，但有函数参数
            if (delta.FunctionArguments != null)
            {
                if (collected.Count > 0)
                {
                    var last = collected[collected.Count - 1];
                    last.FunctionArguments = (last.FunctionArguments ?? "") + delta.FunctionArguments;
                    if (delta.FunctionName != null)
                    {
                        last.FunctionName = delta.FunctionName;
                    }
                }
                else
                {
                    // 第一个分片无 Id 无 Index，创建新条目避免数据丢失
                    collected.Add(new ToolCallRequest
                    {
                        FunctionName = delta.FunctionName,
                        FunctionArguments = delta.FunctionArguments
                    });
                }
            }
        }

        /// <summary>
        /// 将 delta 的工具调用数据合并到已有条目
        /// </summary>
        private static void MergeDelta(ToolCallRequest existing, ToolCallRequest delta)
        {
            if (delta.FunctionName != null)
            {
                existing.FunctionName = delta.FunctionName;
            }
            if (delta.FunctionArguments != null)
            {
                existing.FunctionArguments = (existing.FunctionArguments ?? "") + delta.FunctionArguments;
            }
            // 如果 delta 带有 Id 但 existing 没有，补充 Id
            if (!string.IsNullOrEmpty(delta.Id) && string.IsNullOrEmpty(existing.Id))
            {
                existing.Id = delta.Id;
            }
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
        /// 回滚最后一条用户消息（流式出错时清理历史）
        /// </summary>
        private void RemoveLastUserMessage()
        {
            lock (_historyLock)
            {
                if (_conversationHistory.Count > 0)
                {
                    var last = _conversationHistory[_conversationHistory.Count - 1];
                    if (last.Role == MessageRole.User)
                    {
                        _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前对话历史的副本
        /// </summary>
        /// <returns>对话消息列表</returns>
        public List<ConversationMessage> GetHistory()
        {
            lock (_historyLock)
            {
                return new List<ConversationMessage>(_conversationHistory);
            }
        }
    }
}
