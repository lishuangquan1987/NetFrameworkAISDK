namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 结构化输出格式配置
    /// </summary>
    public class ResponseFormat
    {
        /// <summary>
        /// 输出类型（"json_schema" 或 "json_object"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// JSON Schema 字符串
        /// </summary>
        public string JsonSchema { get; set; }

        /// <summary>
        /// Schema 名称（OpenAI strict mode 必需）
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// 是否启用严格模式（OpenAI strict mode）
        /// </summary>
        public bool Strict { get; set; }
    }
}