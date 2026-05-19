using System;

namespace NetFrameworkAI.Common
{
    /// <summary>
    /// API 错误基类
    /// </summary>
    public class ApiError
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public ApiError()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误消息</param>
        public ApiError(string message)
        {
            Message = message;
        }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int? HttpStatusCode { get; set; }
    }

    /// <summary>
    /// 包含结果或错误的响应
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 结果数据
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public ApiError Error { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get { return Error == null; } }
    }
}
