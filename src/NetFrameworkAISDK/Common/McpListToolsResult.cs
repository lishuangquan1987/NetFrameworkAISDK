using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP tools/list 响应中的结果
    /// </summary>
    internal class McpListToolsResult
    {
        /// <summary>可用工具列表</summary>
        public List<McpToolDefinition> Tools { get; set; }
    }
}