namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 函数定义，包含函数名称、描述和参数 Schema
    /// </summary>
    public class FunctionDefinition
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 参数定义（JSON Schema 格式）
        /// </summary>
        public object Parameters { get; set; }
    }
}
