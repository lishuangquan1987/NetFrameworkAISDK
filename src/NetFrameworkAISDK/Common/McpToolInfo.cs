using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP（Model Control Protocol）工具信息，包含工具的元数据
    /// </summary>
    public class McpToolInfo
    {
        /// <summary>
        /// 工具名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 输入参数 Schema（JSON Schema 格式）
        /// </summary>
        public Dictionary<string, object> InputSchema { get; set; }
    }
}