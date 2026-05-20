using System.Collections.Generic;
using NetFrameworkAISDK.OpenAI;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// 消息创建请求
    /// </summary>
    public class MessagesRequest
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 消息列表
        /// </summary>
        public List<AnthropicMessage> Messages { get; set; }

        /// <summary>
        /// 系统提示
        /// </summary>
        public string System { get; set; }

        /// <summary>
        /// 最大 token 数
        /// </summary>
        public int MaxTokens { get; set; }

        /// <summary>
        /// 是否流式输出
        /// </summary>
        public bool? Stream { get; set; }

        /// <summary>
        /// 温度参数（0-1）
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// 工具列表
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }
    }

    /// <summary>
    /// 消息创建响应
    /// </summary>
    public class MessagesResponse
    {
        /// <summary>
        /// 对象类型（通常是 "message"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 消息 ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 角色（通常是 "assistant"）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 内容块列表
        /// </summary>
        public List<ContentBlock> Content { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 停止原因
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// 停止序列
        /// </summary>
        public string StopSequence { get; set; }

        /// <summary>
        /// 使用统计
        /// </summary>
        public AnthropicUsage Usage { get; set; }
    }

    /// <summary>
    /// 流式事件类型
    /// </summary>
    public static class StreamEventType
    {
        public const string MessageStart = "message_start";
        public const string ContentBlockStart = "content_block_start";
        public const string ContentBlockDelta = "content_block_delta";
        public const string ContentBlockStop = "content_block_stop";
        public const string MessageDelta = "message_delta";
        public const string MessageStop = "message_stop";
    }

    /// <summary>
    /// 流式事件基类
    /// </summary>
    public class StreamEvent
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 消息数据（message_start 事件）
        /// </summary>
        public MessagesResponse Message { get; set; }

        /// <summary>
        /// 索引（content_block_* 事件）
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// 内容块（content_block_start 事件）
        /// </summary>
        public ContentBlock ContentBlock { get; set; }

        /// <summary>
        /// 增量数据（content_block_delta 事件）
        /// </summary>
        public Delta Delta { get; set; }

        /// <summary>
        /// 消息增量（message_delta 事件）
        /// </summary>
        public MessageDeltaData DeltaMessage { get; set; }

        /// <summary>
        /// 使用统计增量（message_delta 事件）
        /// </summary>
        public AnthropicUsage Usage { get; set; }
    }

    /// <summary>
    /// 增量数据
    /// </summary>
    public class Delta
    {
        /// <summary>
        /// 类型（通常是 "text_delta"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本增量
        /// </summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// 消息增量数据
    /// </summary>
    public class MessageDeltaData
    {
        /// <summary>
        /// 停止原因
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// 停止序列
        /// </summary>
        public string StopSequence { get; set; }
    }

    /// <summary>
    /// Anthropic 使用统计
    /// </summary>
    public class AnthropicUsage
    {
        /// <summary>
        /// 输入 token 数
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// 输出 token 数
        /// </summary>
        public int OutputTokens { get; set; }
    }
}
