using System.Collections.Generic;

namespace NetFrameworkAI.OpenAI
{
    /// <summary>
    /// 聊天消息角色
    /// </summary>
    public static class ChatRole
    {
        public const string System = "system";
        public const string User = "user";
        public const string Assistant = "assistant";
        public const string Tool = "tool";
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 工具调用名称（仅 tool 角色）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具调用 ID（仅 tool 角色）
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 工具调用列表（仅 assistant 角色）
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }
    }

    /// <summary>
    /// 工具调用
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// 工具调用 ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 工具类型（通常是 function）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 函数信息
        /// </summary>
        public FunctionCall Function { get; set; }
    }

    /// <summary>
    /// 函数调用
    /// </summary>
    public class FunctionCall
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数参数（JSON 字符串）
        /// </summary>
        public string Arguments { get; set; }
    }

    /// <summary>
    /// 工具定义
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// 工具类型（通常是 function）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 函数定义
        /// </summary>
        public FunctionDefinition Function { get; set; }
    }

    /// <summary>
    /// 函数定义
    /// </summary>
    public class FunctionDefinition
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 参数定义（JSON Schema）
        /// </summary>
        public object Parameters { get; set; }
    }
}
