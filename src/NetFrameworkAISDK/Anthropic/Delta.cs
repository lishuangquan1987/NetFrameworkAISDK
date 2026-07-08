namespace NetFrameworkAISDK.Anthropic
{
    /// <summary>
    /// Anthropic 增量数据，用于流式传输中的内容和状态变化
    /// </summary>
    public class Delta
    {
        /// <summary>
        /// 类型（"text_delta"、"input_json_delta" 或 null 表示 message_delta）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本增量（text_delta 类型时使用）
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 工具输入 JSON 增量（input_json_delta 类型时使用）
        /// </summary>
        public string PartialJson { get; set; }

        /// <summary>
        /// 停止原因（message_delta 事件时使用）
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// 停止序列（message_delta 事件时使用）
        /// </summary>
        public string StopSequence { get; set; }

        /// <summary>
        /// 思考/推理增量文本（thinking_delta 类型时使用）
        /// </summary>
        public string Thinking { get; set; }

        /// <summary>
        /// 思考签名（thinking_delta 最后一块时填充）
        /// </summary>
        public string Signature { get; set; }
    }
}