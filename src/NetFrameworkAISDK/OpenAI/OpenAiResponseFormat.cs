namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 响应格式配置（映射到 API 的 response_format 字段）
    /// </summary>
    public class OpenAiResponseFormat
    {
        /// <summary>
        /// 响应格式类型（"json_object" 或 "json_schema"）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// JSON Schema 配置（当 Type 为 "json_schema" 时使用）
        /// </summary>
        public JsonSchemaObject JsonSchema { get; set; }
    }

    /// <summary>
    /// OpenAI JSON Schema 对象（嵌套在 OpenAiResponseFormat 中）
    /// </summary>
    public class JsonSchemaObject
    {
        /// <summary>
        /// Schema 名称（最多 64 字符，a-z/A-Z/0-9/_/-）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否启用严格模式
        /// </summary>
        public bool Strict { get; set; }

        /// <summary>
        /// JSON Schema 定义（object 类型）
        /// </summary>
        public object Schema { get; set; }
    }
}