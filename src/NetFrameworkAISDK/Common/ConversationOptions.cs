using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 对话配置选项
    /// </summary>
    public class ConversationOptions
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 系统提示词
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// 最大 Token 数，null 使用默认值
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// 温度参数（0-2），控制回复随机性，null 使用默认值
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// 本次对话临时附加的工具列表
        /// </summary>
        public List<AIFunction> Tools { get; set; }

        /// <summary>
        /// 是否启用流式输出
        /// </summary>
        public bool Stream { get; set; }
    }
}