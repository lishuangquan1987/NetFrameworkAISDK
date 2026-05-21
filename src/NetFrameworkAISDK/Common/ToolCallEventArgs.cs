using System;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 工具调用事件参数，包含工具调用的完整上下文信息
    /// </summary>
    public class ToolCallEventArgs : EventArgs
    {
        /// <summary>
        /// 工具/函数名称
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 函数参数（JSON 字符串）
        /// </summary>
        public string FunctionArguments { get; set; }

        /// <summary>
        /// 工具执行返回的结果
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// 工具调用 ID
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 是否需要用户审批
        /// </summary>
        public bool RequiresApproval { get; set; }

        /// <summary>
        /// 审批状态：null=未决定, true=已批准, false=已拒绝
        /// </summary>
        public bool? IsApproved { get; set; }
    }
}