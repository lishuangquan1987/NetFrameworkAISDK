using System;
using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins
{
    /// <summary>
    /// Agent 执行上下文，贯穿整个中间件管道
    /// </summary>
    public class AgentContext
    {
        private readonly Dictionary<string, object> _items;

        /// <summary>
        /// 当前 AIAgent 实例
        /// </summary>
        public AIAgent Agent { get; internal set; }

        /// <summary>
        /// 用户消息
        /// </summary>
        public string UserMessage { get; set; }

        /// <summary>
        /// 多模态内容（图片等）
        /// </summary>
        public List<MessageContent> ContentParts { get; set; }

        /// <summary>
        /// 对话选项
        /// </summary>
        public ConversationOptions Options { get; set; }

        /// <summary>
        /// AI 响应（可被中间件修改）
        /// </summary>
        public ApiResponse<string> Response { get; set; }

        /// <summary>
        /// 当前对话历史（可被中间件修改）
        /// </summary>
        public List<ConversationMessage> ConversationHistory { get; internal set; }

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime StartTime { get; internal set; }

        /// <summary>
        /// 请求 ID（用于追踪）
        /// </summary>
        public string RequestId { get; internal set; }

        /// <summary>
        /// 中间件之间共享的数据字典
        /// </summary>
        public Dictionary<string, object> Items
        {
            get { return _items; }
        }

        public AgentContext()
        {
            _items = new Dictionary<string, object>();
            StartTime = DateTime.UtcNow;
            RequestId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 获取共享数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">键</param>
        /// <returns>值，不存在返回 default(T)</returns>
        public T GetItem<T>(string key)
        {
            object value;
            if (_items.TryGetValue(key, out value))
            {
                if (value is T)
                {
                    return (T)value;
                }
            }
            return default(T);
        }

        /// <summary>
        /// 设置共享数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void SetItem(string key, object value)
        {
            _items[key] = value;
        }

        /// <summary>
        /// 获取执行耗时
        /// </summary>
        /// <returns>耗时（毫秒）</returns>
        public long GetElapsedMilliseconds()
        {
            return (long)(DateTime.UtcNow - StartTime).TotalMilliseconds;
        }
    }

    /// <summary>
    /// 中间件执行结果
    /// </summary>
    public class MiddlewareResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public ApiResponse<string> Response { get; set; }

        public static MiddlewareResult Success(ApiResponse<string> response)
        {
            return new MiddlewareResult { IsSuccess = true, Response = response };
        }

        public static MiddlewareResult Failure(string error)
        {
            return new MiddlewareResult { IsSuccess = false, ErrorMessage = error };
        }
    }
}
