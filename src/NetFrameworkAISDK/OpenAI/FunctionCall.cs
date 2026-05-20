namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 函数调用，包含函数名称和参数
    /// </summary>
    public class FunctionCall
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数参数（JSON 字符串格式）
        /// </summary>
        public string Arguments { get; set; }
    }
}