using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// 聊天完成请求
    /// </summary>
    public class ChatCompletionRequest
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 消息列表
        /// </summary>
        public List<ChatMessage> Messages { get; set; }

        /// <summary>
        /// 是否流式输出
        /// </summary>
        public bool? Stream { get; set; }

        /// <summary>
        /// 温度参数（0-2）
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// 最大生成 token 数
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// 工具列表
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }
    }

    /// <summary>
    /// 聊天完成响应
    /// </summary>
    public class ChatCompletionResponse
    {
        /// <summary>
        /// 响应 ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// 创建时间戳
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 选择列表
        /// </summary>
        public List<ChatChoice> Choices { get; set; }

        /// <summary>
        /// 使用统计
        /// </summary>
        public UsageInfo Usage { get; set; }
    }

    /// <summary>
    /// 聊天选择
    /// </summary>
    public class ChatChoice
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public ChatMessage Message { get; set; }

        /// <summary>
        /// 完成原因
        /// </summary>
        public string FinishReason { get; set; }
    }

    /// <summary>
    /// 流式聊天选择
    /// </summary>
    public class ChatStreamChoice
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 增量消息
        /// </summary>
        public ChatMessage Delta { get; set; }

        /// <summary>
        /// 完成原因
        /// </summary>
        public string FinishReason { get; set; }
    }

    /// <summary>
    /// 流式聊天完成响应
    /// </summary>
    public class ChatCompletionStreamResponse
    {
        /// <summary>
        /// 响应 ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// 创建时间戳
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 选择列表
        /// </summary>
        public List<ChatStreamChoice> Choices { get; set; }
    }

    /// <summary>
    /// 使用统计
    /// </summary>
    public class UsageInfo
    {
        /// <summary>
        /// 提示词 token 数
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// 完成 token 数
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// 总 token 数
        /// </summary>
        public int TotalTokens { get; set; }
    }
}
