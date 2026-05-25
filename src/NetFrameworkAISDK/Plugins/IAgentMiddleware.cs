using System;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// Agent 中间件接口，允许在 Agent 执行过程中拦截和修改请求/响应
    /// </summary>
    public interface IAgentMiddleware
    {
        /// <summary>
        /// 中间件名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 执行顺序（数字越小越先执行）
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        /// <param name="context">Agent 上下文</param>
        /// <param name="next">下一个中间件或 Agent 核心的委托</param>
        /// <returns>执行结果</returns>
        ApiResponse<string> Invoke(AgentContext context, Func<ApiResponse<string>> next);
    }

    /// <summary>
    /// 中间件插件接口
    /// </summary>
    public interface IMiddlewarePlugin : IPlugin
    {
        /// <summary>
        /// 中间件类型标识
        /// </summary>
        string MiddlewareType { get; }

        /// <summary>
        /// 创建中间件实例
        /// </summary>
        /// <param name="config">插件配置</param>
        /// <returns>中间件实例</returns>
        IAgentMiddleware CreateMiddleware(PluginConfig config);
    }

    /// <summary>
    /// 中间件基类，提供通用功能
    /// </summary>
    public abstract class AgentMiddlewareBase : IAgentMiddleware
    {
        public abstract string Name { get; }
        public virtual int Order { get { return 100; } }

        public abstract ApiResponse<string> Invoke(AgentContext context, Func<ApiResponse<string>> next);

        protected ApiResponse<string> Continue(AgentContext context, Func<ApiResponse<string>> next)
        {
            return next();
        }
    }

    /// <summary>
    /// 日志中间件基类
    /// </summary>
    public abstract class LoggingMiddlewareBase : AgentMiddlewareBase
    {
        protected readonly Action<string> Logger;

        protected LoggingMiddlewareBase(Action<string> logger)
        {
            Logger = logger ?? (_ => { });
        }

        protected void LogInfo(string message)
        {
            Logger("[INFO] " + message);
        }

        protected void LogWarning(string message)
        {
            Logger("[WARN] " + message);
        }

        protected void LogError(string message)
        {
            Logger("[ERROR] " + message);
        }
    }
}
