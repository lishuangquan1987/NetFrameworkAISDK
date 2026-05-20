using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 工具调用请求，描述模型请求调用的单个工具/函数
    /// </summary>
    public class ToolCallRequest
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
        /// 函数名称
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 函数参数（JSON 字符串格式）
        /// </summary>
        public string FunctionArguments { get; set; }
    }
}