using NetFrameworkAI.OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace NetFrameworkAI.Common
{
    /// <summary>
    /// AI 函数定义
    /// </summary>
    public class AIFunction
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
        /// 函数定义（JSON Schema）
        /// </summary>
        public object Parameters { get; set; }

        /// <summary>
        /// 函数执行委托
        /// </summary>
        public Func<string, string> Execute { get; set; }

        /// <summary>
        /// 转换为 OpenAI 工具定义
        /// </summary>
        /// <returns>OpenAI 工具定义</returns>
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

    /// <summary>
    /// AI 函数工厂类
    /// </summary>
    public static class AIFunctionFactory
    {
        /// <summary>
        /// 从方法创建 AI 函数（MAF 风格，支持方法组直接传递）
        /// </summary>
        /// <param name="function">方法引用（支持方法组）</param>
        /// <returns>AI 函数</returns>
        public static AIFunction Create(Delegate function)
        {
            return Create(function.Method, function.Target);
        }

        /// <summary>
        /// 从方法创建 AI 函数
        /// </summary>
        /// <param name="method">方法信息</param>
        /// <param name="target">目标对象（静态方法为 null）</param>
        /// <returns>AI 函数</returns>
        public static AIFunction Create(MethodInfo method, object target)
        {
            var descriptionAttr = GetDescriptionAttribute(method);
            var parameters = BuildParametersSchema(method);

            return new AIFunction
            {
                Name = method.Name,
                Description = descriptionAttr != null ? descriptionAttr.Description : method.Name,
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson => ExecuteFunction(method, target, argsJson))
            };
        }

        /// <summary>
        /// 从委托创建 AI 函数
        /// </summary>
        /// <param name="func">委托</param>
        /// <param name="description">函数描述</param>
        /// <returns>AI 函数</returns>
        public static AIFunction Create(Func<string, string> func, string description)
        {
            var method = func.Method;
            var parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", new Dictionary<string, object>
                    {
                        { "input", new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", description }
                            }
                        }
                    }
                },
                { "required", new List<string> { "input" } }
            };

            return new AIFunction
            {
                Name = method.Name,
                Description = description,
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson =>
                {
                    var args = JsonHelper.Deserialize<Dictionary<string, string>>(argsJson);
                    return args.ContainsKey("input") ? args["input"] : "";
                })
            };
        }

        /// <summary>
        /// 从无参数委托创建 AI 函数
        /// </summary>
        /// <param name="func">委托</param>
        /// <param name="description">函数描述</param>
        /// <returns>AI 函数</returns>
        public static AIFunction Create(Func<string> func, string description)
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
                Name = method.Name,
                Description = description,
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson => func())
            };
        }

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

        private static DescriptionAttribute GetDescriptionAttribute(MemberInfo member)
        {
            var attributes = member.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? (DescriptionAttribute)attributes[0] : null;
        }

        private static DescriptionAttribute GetDescriptionAttribute(ParameterInfo parameter)
        {
            var attributes = parameter.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? (DescriptionAttribute)attributes[0] : null;
        }

        private static object BuildParametersSchema(MethodInfo method)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var param in method.GetParameters())
            {
                var paramName = param.Name;
                var paramType = GetJsonType(param.ParameterType);
                var descriptionAttr = GetDescriptionAttribute(param);

                var property = new Dictionary<string, object>
                {
                    { "type", paramType }
                };

                if (descriptionAttr != null)
                {
                    property["description"] = descriptionAttr.Description;
                }

                properties[paramName] = property;

                if (!param.IsOptional)
                {
                    required.Add(paramName);
                }
            }

            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "required", required }
            };
        }

        private static string GetJsonType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return "array";
            return "object";
        }

        private static string ExecuteFunction(MethodInfo method, object target, string argsJson)
        {
            try
            {
                var argsDict = JsonHelper.Deserialize<Dictionary<string, object>>(argsJson);
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    object value;
                    if (argsDict.TryGetValue(param.Name, out value))
                    {
                        args[i] = Convert.ChangeType(value, param.ParameterType);
                    }
                    else if (param.IsOptional)
                    {
                        args[i] = param.DefaultValue;
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("Missing required parameter: {0}", param.Name));
                    }
                }

                var result = method.Invoke(target, args);
                return result != null ? result.ToString() : "";
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
        }
    }
}
