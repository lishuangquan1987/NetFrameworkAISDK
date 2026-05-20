namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP JSON-RPC 响应对象
    /// </summary>
    internal class McpJsonRpcResponse
    {
        /// <summary>JSON-RPC 版本号</summary>
        public string Jsonrpc { get; set; }

        /// <summary>响应 ID，与请求 ID 对应</summary>
        public int? Id { get; set; }

        /// <summary>成功时的结果数据</summary>
        public object Result { get; set; }

        /// <summary>失败时的错误信息</summary>
        public McpJsonRpcError Error { get; set; }

        /// <summary>是否包含错误</summary>
        public bool HasError
        {
            get { return Error != null; }
        }
    }
}