namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// API 响应包装类，统一封装错误信息和结果数据
    /// </summary>
    /// <typeparam name="T">结果数据类型</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 成功时返回的结果数据
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// 失败时返回的错误信息（null 表示成功）
        /// </summary>
        public ApiError Error { get; set; }

        /// <summary>
        /// 是否成功（Error 为 null 时表示成功）
        /// </summary>
        public bool IsSuccess
        {
            get { return Error == null; }
        }
    }
}