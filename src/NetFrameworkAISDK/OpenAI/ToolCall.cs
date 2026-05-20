namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 工具调用，描述模型请求调用的单个工具
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// 工具调用唯一标识符
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 工具类型（通常为 "function"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 函数调用信息
        /// </summary>
        public FunctionCall Function { get; set; }
    }
}