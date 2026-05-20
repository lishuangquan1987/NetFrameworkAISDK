using System;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// API 错误信息
    /// </summary>
    public class ApiError
    {
        /// <summary>
        /// 创建空的错误对象
        /// </summary>
        public ApiError()
        {
        }

        /// <summary>
        /// 创建带消息的错误对象
        /// </summary>
        /// <param name="message">错误描述消息</param>
        public ApiError(string message)
        {
            Message = message;
        }

        /// <summary>
        /// 错误描述消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误类型标识
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// HTTP 状态码（仅当错误来源于 HTTP 请求时有效）
        /// </summary>
        public int? HttpStatusCode { get; set; }
    }
}