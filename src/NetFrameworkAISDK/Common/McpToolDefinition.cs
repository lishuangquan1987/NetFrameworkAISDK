using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP 工具定义（来自 tools/list 响应）
    /// </summary>
    internal class McpToolDefinition
    {
        /// <summary>工具名称</summary>
        public string Name { get; set; }

        /// <summary>工具描述</summary>
        public string Description { get; set; }

        /// <summary>输入参数 Schema（JSON Schema 格式）</summary>
        public Dictionary<string, object> InputSchema { get; set; }
    }
}