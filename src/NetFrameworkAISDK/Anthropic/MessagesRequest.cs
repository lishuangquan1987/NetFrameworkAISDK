using System.Collections.Generic;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 消息创建请求
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
        /// 最大生成 token 数
        /// </summary>
        public int MaxTokens { get; set; }

        /// <summary>
        /// 是否启用流式输出
        /// </summary>
        public bool? Stream { get; set; }

        /// <summary>
        /// 温度参数（0-1），控制回复随机性
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// 可用工具列表
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }
    }
}