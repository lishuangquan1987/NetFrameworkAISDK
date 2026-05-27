using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// AI 函数工厂，从 MethodInfo 创建 AIFunction 实例，自动提取特性注解
    /// </summary>
    public static class AIFunctionFactory
    {
        /// <summary>
        /// 从委托创建 AI 函数（便捷重载，自动提取 MethodInfo 和 target）
        /// </summary>
        /// <param name="del">委托实例</param>
        /// <returns>AIFunction 实例</returns>
        public static AIFunction Create(Delegate del)
        {
            return Create(del.Method, del.Target);
        }

        /// <summary>
        /// 从委托创建 AI 函数并指定自定义描述
        /// </summary>
        /// <param name="del">委托实例</param>
        /// <param name="description">自定义函数描述</param>
        /// <returns>AIFunction 实例</returns>
        public static AIFunction Create(Delegate del, string description)
        {
            var func = Create(del.Method, del.Target);
            func.Description = description;
            return func;
        }

        /// <summary>
        /// 从单个方法创建 AI 函数
        /// </summary>
        /// <param name="method">要封装的方法</param>
        /// <param name="target">方法所属对象实例（静态方法传 null）</param>
        /// <returns>AIFunction 实例</returns>
        public static AIFunction Create(MethodInfo method, object target)
        {
            var name = method.Name;
            var descAttr = GetDescriptionAttribute(method);
            var description = descAttr != null ? descAttr.Description : name;

            var parameters = BuildParametersSchema(method);

            return new AIFunction
            {
                Name = name,
                Description = description,
                Parameters = parameters,
                Execute = new Func<string, string>(argsJson => InvokeMethod(method, target, argsJson))
            };
        }

        /// <summary>
        /// 从类型的所有公共方法创建 AI 函数列表
        /// </summary>
        /// <param name="type">包含工具方法的类型</param>
        /// <param name="target">类型实例（静态方法传 null）</param>
        /// <returns>AIFunction 列表</returns>
        public static List<AIFunction> CreateFromType(Type type, object target)
        {
            var functions = new List<AIFunction>();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0)
                {
                    functions.Add(Create(method, target));
                }
            }

            return functions;
        }

        /// <summary>
        /// 从方法参数构建 JSON Schema 格式的参数定义
        /// </summary>
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

        /// <summary>
        /// 将 CLR 类型映射为 JSON Schema 类型字符串
        /// </summary>
        private static string GetJsonType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return "array";
            return "object";
        }

        /// <summary>
        /// 通过反射调用方法
        /// </summary>
        /// <param name="method">目标方法</param>
        /// <param name="target">目标对象实例</param>
        /// <param name="argsJson">参数 JSON 字符串</param>
        /// <returns>方法返回值字符串</returns>
        internal static string InvokeMethod(MethodInfo method, object target, string argsJson)
        {
            try
            {
                if (argsJson == null)
                {
                    return "Error: argsJson is null";
                }

                var argsDict = JsonHelper.Deserialize<Dictionary<string, object>>(argsJson);
                if (argsDict == null)
                {
                    return "Error: Failed to deserialize args: " + (argsJson.Length > 200 ? argsJson.Substring(0, 200) + "..." : argsJson);
                }
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    object value;
                    if (argsDict.TryGetValue(param.Name, out value) && value != null)
                    {
                        var targetType = param.ParameterType;
                        if (targetType == typeof(string) || targetType.IsPrimitive || targetType == typeof(decimal))
                        {
                            args[i] = Convert.ChangeType(value, targetType);
                        }
                        else if (targetType.IsEnum)
                        {
                            args[i] = Enum.Parse(targetType, value.ToString());
                        }
                        else if (typeof(IConvertible).IsAssignableFrom(targetType))
                        {
                            args[i] = Convert.ChangeType(value, targetType);
                        }
                        else
                        {
                            var json = JsonHelper.Serialize(value);
                            args[i] = JsonHelper.Deserialize(json, targetType);
                        }
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

                // 参数完整性检查：避免 null 传入方法导致 NRE
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null)
                    {
                        if (parameters[i].IsOptional)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else
                        {
                            throw new ArgumentException(string.Format("Parameter '{0}' is null and not optional", parameters[i].Name));
                        }
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

        /// <summary>
        /// 从成员获取 Description 特性
        /// </summary>
        private static DescriptionAttribute GetDescriptionAttribute(MemberInfo member)
        {
            var attributes = member.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? (DescriptionAttribute)attributes[0] : null;
        }

        /// <summary>
        /// 从参数获取 Description 特性
        /// </summary>
        private static DescriptionAttribute GetDescriptionAttribute(ParameterInfo parameter)
        {
            var attributes = parameter.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? (DescriptionAttribute)attributes[0] : null;
        }
    }
}
