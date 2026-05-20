using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 聊天消息，包含文本、图像内容和工具调用信息
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 角色（参见 <see cref="ChatRole"/> 常量）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容（简单文本消息使用）
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 多模态内容块列表（用于图像等复杂内容）
        /// </summary>
        public List<ImageContentPart> ContentParts { get; set; }

        /// <summary>
        /// 工具调用名称（仅 tool 角色）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具调用 ID（仅 tool 角色，关联到对应的工具调用请求）
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 工具调用列表（仅 assistant 角色）
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }
    }
}