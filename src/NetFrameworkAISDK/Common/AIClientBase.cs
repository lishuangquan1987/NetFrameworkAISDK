using System;
using System.Collections.Generic;
using NetFrameworkAISDK.OpenAI;

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
            ConversationOptions options);

        /// <inheritdoc />
        public abstract void SendConversationStreaming(
            List<ConversationMessage> messages,
            ConversationOptions options,
            Action<ConversationResponse> onChunk,
            Action<ApiError> onError);

        /// <inheritdoc />
        public virtual void ConfigureTools(IEnumerable<AIFunction> tools)
        {
            _tools = tools != null ? new List<AIFunction>(tools) : new List<AIFunction>();
            _toolMap = new Dictionary<string, AIFunction>();
            if (tools != null)
            {
                foreach (var t in tools)
                {
                    if (t != null && !string.IsNullOrEmpty(t.Name))
                    {
                        _toolMap[t.Name] = t;
                    }
                }
            }
        }

        /// <summary>
        /// 构建 OpenAI ToolDefinition 列表，合并全局工具和对话选项中的临时工具
        /// </summary>
        /// <param name="options">对话选项</param>
        /// <returns>工具定义列表，无工具时返回 null</returns>
        protected List<ToolDefinition> BuildToolDefinitions(ConversationOptions options)
        {
            var allTools = new List<AIFunction>();
            if (_tools != null)
            {
                allTools.AddRange(_tools);
            }
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
        /// 按名称查找已注册的工具函数
        /// </summary>
        /// <param name="name">工具名称</param>
        /// <returns>找到的 AIFunction，未找到返回 null</returns>
        protected AIFunction FindTool(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            AIFunction result;
            _toolMap.TryGetValue(name, out result);
            return result;
        }

        /// <summary>
        /// 按名称执行已注册的工具函数
        /// </summary>
        /// <param name="functionName">函数名称</param>
        /// <param name="functionArgs">函数参数 JSON 字符串</param>
        /// <returns>函数执行结果</returns>
        public string ExecuteTool(string functionName, string functionArgs)
        {
            var function = FindTool(functionName);
            if (function != null)
            {
                return function.Execute(functionArgs);
            }
            return "Error: Tool '" + functionName + "' not found.";
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