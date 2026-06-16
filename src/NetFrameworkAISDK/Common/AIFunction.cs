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
        private Dictionary<string, object> _parametersDictionary;

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
        public object Parameters
        {
            get { return _parametersDictionary; }
            set
            {
                var dict = value as Dictionary<string, object>;
                if (dict != null)
                {
                    _parametersDictionary = dict;
                }
                else
                {
                    // 保持向后兼容
                    _parametersDictionary = value as Dictionary<string, object>;
                }
            }
        }

        /// <summary>
        /// 函数参数定义（强类型，JSON Schema 格式）
        /// </summary>
        public Dictionary<string, object> ParametersSchema
        {
            get { return _parametersDictionary; }
            set { _parametersDictionary = value; }
        }

        /// <summary>
        /// 执行回调，接收参数 JSON 字符串，返回结果字符串
        /// </summary>
        public Func<string, string> Execute { get; set; }

        /// <summary>
        /// 是否需要用户确认后才能执行。为 true 时 Agent 会暂停等待 ToolApproval 回调审批。
        /// </summary>
        public bool RequiresApproval { get; set; }

        /// <summary>
        /// 动态审批判断函数（可选，优先级高于 RequiresApproval）。
        /// 参数：(functionName, functionArguments_json) → 是否需要审批
        /// </summary>
        public Func<string, string, bool> ApprovalPredicate { get; set; }

        /// <summary>
        /// 创建 AI 函数实例
        /// </summary>
        public AIFunction()
        {
            _parametersDictionary = new Dictionary<string, object>();
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

            var result = new AIFunction();
            result.Name = !string.IsNullOrEmpty(name) ? name : AIFunctionFactory.GetCleanMethodName(method);
            result.Description = description;
            result.ParametersSchema = parameters;
            result.Execute = new Func<string, string>(argsJson => func());
            return result;
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
