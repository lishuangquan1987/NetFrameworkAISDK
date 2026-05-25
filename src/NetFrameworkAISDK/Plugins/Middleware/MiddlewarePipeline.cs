using System;
using System.Collections.Generic;
using System.Linq;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// 中间件管道，管理多个中间件的执行顺序和调用
    /// </summary>
    public class MiddlewarePipeline
    {
        private readonly List<IAgentMiddleware> _middlewares;
        private readonly object _lock = new object();

        public MiddlewarePipeline()
        {
            _middlewares = new List<IAgentMiddleware>();
        }

        /// <summary>
        /// 添加中间件到管道
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        /// <returns>管道本身，支持链式调用</returns>
        public MiddlewarePipeline Use(IAgentMiddleware middleware)
        {
            if (middleware == null)
            {
                throw new ArgumentNullException("middleware");
            }

            lock (_lock)
            {
                _middlewares.Add(middleware);
                _middlewares.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            return this;
        }

        /// <summary>
        /// 添加多个中间件到管道
        /// </summary>
        /// <param name="middlewares">中间件列表</param>
        public void UseRange(IEnumerable<IAgentMiddleware> middlewares)
        {
            if (middlewares == null)
            {
                return;
            }

            lock (_lock)
            {
                _middlewares.AddRange(middlewares);
                _middlewares.Sort((a, b) => a.Order.CompareTo(b.Order));
            }
        }

        /// <summary>
        /// 清空所有中间件
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _middlewares.Clear();
            }
        }

        /// <summary>
        /// 获取中间件数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _middlewares.Count;
                }
            }
        }

        /// <summary>
        /// 执行管道
        /// </summary>
        /// <param name="context">Agent 上下文</param>
        /// <param name="handler">最终处理器（Agent 核心逻辑）</param>
        /// <returns>执行结果</returns>
        public Common.ApiResponse<string> Execute(AgentContext context, Func<Common.ApiResponse<string>> handler)
        {
            List<IAgentMiddleware> middlewaresCopy;
            lock (_lock)
            {
                middlewaresCopy = _middlewares.ToList();
            }

            if (middlewaresCopy.Count == 0)
            {
                return handler();
            }

            Func<Common.ApiResponse<string>> pipeline = handler;

            for (int i = middlewaresCopy.Count - 1; i >= 0; i--)
            {
                var middleware = middlewaresCopy[i];
                var next = pipeline;
                pipeline = () => middleware.Invoke(context, next);
            }

            return pipeline();
        }

        /// <summary>
        /// 获取中间件列表
        /// </summary>
        /// <returns>中间件列表</returns>
        public IEnumerable<IAgentMiddleware> GetMiddlewares()
        {
            lock (_lock)
            {
                return _middlewares.ToList();
            }
        }

        /// <summary>
        /// 移除指定名称的中间件
        /// </summary>
        /// <param name="middlewareName">中间件名称</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(string middlewareName)
        {
            lock (_lock)
            {
                var middleware = _middlewares.Find(m => 
                    string.Equals(m.Name, middlewareName, StringComparison.OrdinalIgnoreCase));
                
                if (middleware != null)
                {
                    return _middlewares.Remove(middleware);
                }
                return false;
            }
        }

        /// <summary>
        /// 获取中间件执行顺序描述
        /// </summary>
        /// <returns>顺序描述字符串</returns>
        public string GetPipelineDescription()
        {
            lock (_lock)
            {
                var parts = new List<string>();
                foreach (var middleware in _middlewares)
                {
                    parts.Add(string.Format("{0} (Order: {1})", middleware.Name, middleware.Order));
                }
                return string.Join(" -> ", parts.ToArray());
            }
        }
    }
}
