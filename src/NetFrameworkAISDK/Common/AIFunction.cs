using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI 函数定义，封装可通过工具调用机制调用的方法。
    /// 支持通过委托、MethodInfo 或 MCP 工具创建。
    /// </summary>
    public class AIFunction
    {
        /// <summary>
        /// 函数名称（模型调用时使用）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数描述（帮助模型理解何时调用此函数）
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 函数参数定义（JSON Schema 格式，object 类型以兼容 MCP 工具）
        /// </summary>
        public object Parameters { get; set; }

        /// <summary>
        /// 执行回调，接收参数 JSON 字符串，返回结果字符串
        /// </summary>
        public Func<string, string> Execute { get; set; }

        /// <summary>
        /// 创建 AI 函数实例
        /// </summary>
        public AIFunction()
        {
        }

        /// <summary>
        /// 通过委托创建函数（无参数版本）
        /// </summary>
        /// <param name="func">无参委托</param>
        /// <param name="description">函数描述</param>
        /// <param name="name">函数名称（可选，Lambda 表达式需显式指定）</param>
        /// <returns>AI 函数实例</returns>
        public static AIFunction Create(Func<string> func, string description, string name = null)
        {
            var method = func.Method;
            var parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", new Dictionary<string, object>() },
                { "required", new List<string>() }
            };

            return new AIFunction
            {
                Name = !string.IsNullOrEmpty(name) ? name : method.Name,
                Description = description,
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson => func())
            };
        }

        /// <summary>
        /// 从 MCP 工具创建函数
        /// </summary>
        /// <param name="name">工具名称</param>
        /// <param name="description">工具描述</param>
        /// <param name="inputSchema">参数 Schema（JSON Schema 格式）</param>
        /// <param name="execute">执行回调</param>
        /// <returns>AI 函数实例</returns>
        public static AIFunction CreateFromMcpTool(string name, string description, object inputSchema, Func<string, string> execute)
        {
            return new AIFunction
            {
                Name = name,
                Description = description,
                Parameters = inputSchema,
                Execute = execute
            };
        }

        /// <summary>
        /// 转换为 OpenAI ToolDefinition 格式
        /// </summary>
        /// <returns>工具定义对象</returns>
        public ToolDefinition ToToolDefinition()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = Name,
                    Description = Description,
                    Parameters = Parameters
                }
            };
        }
    }
}