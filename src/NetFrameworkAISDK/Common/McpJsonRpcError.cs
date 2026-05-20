namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP JSON-RPC 错误信息
    /// </summary>
    internal class McpJsonRpcError
    {
        /// <summary>错误代码</summary>
        public int Code { get; set; }

        /// <summary>错误消息</summary>
        public string Message { get; set; }
    }
}