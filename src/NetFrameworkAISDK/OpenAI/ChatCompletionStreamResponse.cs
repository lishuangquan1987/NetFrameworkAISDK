using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 流式聊天完成响应（SSE 事件数据）
    /// </summary>
    public class ChatCompletionStreamResponse
    {
        /// <summary>
        /// 响应唯一标识符
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 对象类型（如 "chat.completion.chunk"）
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// 创建时间戳（Unix 毫秒）
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 流式选择列表（通常只有一个元素）
        /// </summary>
        public List<ChatStreamChoice> Choices { get; set; }
    }
}