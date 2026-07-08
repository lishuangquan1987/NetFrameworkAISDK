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

        /// <summary>
        /// 结构化输出格式（为 null 时不启用结构化输出）
        /// </summary>
        public ResponseFormat ResponseFormat { get; set; }

        /// <summary>
        /// 是否启用思考/推理模式。
        /// null 表示使用模型默认行为，true 强制启用，false 强制关闭。
        /// OpenAI：对应 reasoning_effort 参数，启用时默认 effort 为 "medium"。
        /// Anthropic：对应 thinking block 的 enabled/disabled。
        /// </summary>
        public bool? EnableThinking { get; set; }

        /// <summary>
        /// 思考/推理努力程度（仅 OpenAI 支持）。
        /// 可选值："low"、"medium"、"high"。未设置时默认 "medium"。
        /// 仅当 EnableThinking 不为 false 时生效。
        /// </summary>
        public string ThinkingEffort { get; set; }

        /// <summary>
        /// 思考预算 Token 数（仅 Anthropic 支持）。
        /// 指定 thinking 阶段可消耗的最大 token 数，必须大于 1024。
        /// 仅当 EnableThinking 不为 false 时生效。
        /// </summary>
        public int? ThinkingBudgetTokens { get; set; }
    }
}