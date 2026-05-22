namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 工具定义，描述可被模型调用的工具
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// 工具类型（通常为 "function"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 函数定义
        /// </summary>
        public FunctionDefinition Function { get; set; }
    }
}
