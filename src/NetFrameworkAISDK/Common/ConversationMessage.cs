using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 统一对话消息，屏蔽不同 AI 提供商的底层消息格式差异
    /// </summary>
    public class ConversationMessage
    {
        /// <summary>
        /// 消息角色（参见 <see cref="MessageRole"/> 常量）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 纯文本内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 发送者名称（可选）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具调用 ID（仅 Tool 角色使用，关联到对应的工具调用请求）
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 工具调用列表（仅 Assistant 角色使用）
        /// </summary>
        public List<ToolCallRequest> ToolCalls { get; set; }

        /// <summary>
        /// 多模态内容块列表（用于图像等非文本内容）
        /// </summary>
        public List<MessageContent> ContentParts { get; set; }

        /// <summary>
        /// 推理内容（DeepSeek 思考模式专用，在后续对话中必须原样传回）
        /// </summary>
        public string ReasoningContent { get; set; }
    }
}
