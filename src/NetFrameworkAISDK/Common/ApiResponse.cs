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

        /// <summary>
        /// 响应元信息（如 Token 统计、模型名称等）
        /// </summary>
        public ApiResponseMetadata Metadata { get; set; }
    }

    /// <summary>
    /// API 响应元信息
    /// </summary>
    public class ApiResponseMetadata
    {
        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 输入 Token 数量
        /// </summary>
        public int? PromptTokens { get; set; }

        /// <summary>
        /// 输出 Token 数量
        /// </summary>
        public int? CompletionTokens { get; set; }

        /// <summary>
        /// 总 Token 数量
        /// </summary>
        public int? TotalTokens { get; set; }

        /// <summary>
        /// 完成原因（stop/tool_calls/max_tokens/end_turn 等）
        /// </summary>
        public string FinishReason { get; set; }
    }
}