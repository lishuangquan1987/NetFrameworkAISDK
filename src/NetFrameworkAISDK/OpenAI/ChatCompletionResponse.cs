using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 聊天完成响应
    /// </summary>
    public class ChatCompletionResponse
    {
        /// <summary>
        /// 响应唯一标识符
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 对象类型（如 "chat.completion"）
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
        /// 完成选项列表
        /// </summary>
        public List<ChatChoice> Choices { get; set; }

        /// <summary>
        /// Token 使用统计
        /// </summary>
        public UsageInfo Usage { get; set; }
    }
}