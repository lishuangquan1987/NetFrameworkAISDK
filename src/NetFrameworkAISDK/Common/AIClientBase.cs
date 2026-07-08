using System;
using System.Collections.Generic;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI 客户端抽象基类，提供共享的工具管理和 HTTP 基础设施。
    /// 统一 OpenAI/Anthropic 客户端的公共逻辑，减少代码重复。
    /// </summary>
    public abstract class AIClientBase : HttpClientBase, IAIClient
    {
        /// <summary>已注册的工具函数列表</summary>
        protected List<AIFunction> _tools;

        /// <summary>工具名称到实例的快速查找映射</summary>
        protected Dictionary<string, AIFunction> _toolMap;

        /// <summary>工具集合锁，保护 _tools/_toolMap 的并发读写</summary>
        private readonly object _toolLock = new object();

        /// <summary>
        /// 创建 AI 客户端基类实例
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        protected AIClientBase(string apiKey, string baseUrl)
            : base(apiKey, baseUrl)
        {
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        /// <summary>
        /// 创建 AI 客户端基类实例（带日志）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="logger">日志记录器</param>
        protected AIClientBase(string apiKey, string baseUrl, ILogger logger)
            : base(apiKey, baseUrl, logger)
        {
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        /// <summary>
        /// 创建 AI 客户端基类实例（指定超时）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        protected AIClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
            : base(apiKey, baseUrl, timeoutMilliseconds)
        {
            _tools = new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
        }

        /// <inheritdoc />
        public abstract ApiResponse<ConversationResponse> SendConversation(
            List<ConversationMessage> messages,
            ConversationOptions options,
            CancellationToken? cancellationToken = null);

        /// <inheritdoc />
        public abstract void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError,
            CancellationToken? cancellationToken = null);

        /// <inheritdoc />
        public virtual void ConfigureTools(IEnumerable<AIFunction> tools)
        {
            var newTools = tools != null ? new List<AIFunction>(tools) : new List<AIFunction>();
            var newMap = new Dictionary<string, AIFunction>();
            if (tools != null)
            {
                foreach (var t in tools)
                {
                    if (t != null && !string.IsNullOrEmpty(t.Name))
                    {
                        newMap[t.Name] = t;
                    }
                }
            }
            lock (_toolLock)
            {
                _tools = newTools;
                _toolMap = newMap;
            }
        }

        /// <summary>
        /// 构建 ToolDefinition 列表，合并全局工具和对话选项中的临时工具
        /// </summary>
        /// <param name="options">对话选项</param>
        /// <returns>工具定义列表，无工具时返回 null</returns>
        protected List<ToolDefinition> BuildToolDefinitions(ConversationOptions options)
        {
            List<AIFunction> toolsSnapshot;
            lock (_toolLock)
            {
                toolsSnapshot = _tools != null ? new List<AIFunction>(_tools) : new List<AIFunction>();
            }

            var allTools = new List<AIFunction>();
            allTools.AddRange(toolsSnapshot);
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

        /// <summary>
        /// 释放工具集合资源
        /// </summary>
        public override void Dispose()
        {
            _tools.Clear();
            _toolMap.Clear();
            base.Dispose();
        }
    }
}
