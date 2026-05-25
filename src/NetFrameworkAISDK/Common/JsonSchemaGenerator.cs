using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// JSON Schema 生成器，从 .NET 类型通过反射自动生成 JSON Schema。
    /// 属性名自动转换为 snake_case 以匹配项目序列化策略。
    /// </summary>
    public static class JsonSchemaGenerator
    {
        private static readonly SnakeCaseNamingStrategy _naming = new SnakeCaseNamingStrategy();
        /// <summary>
        /// 从 .NET 类型生成 JSON Schema 字符串
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="schemaName">Schema 名称</param>
        /// <returns>JSON Schema 字符串</returns>
        public static string GenerateFromType(Type type, string schemaName)
        {
            if (type == null)
            {
                return "{}";
            }

            var schema = BuildSchema(type);
            if (schema == null)
            {
                return "{}";
            }

            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                },
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(schema, settings);
        }

        private static Dictionary<string, object> BuildSchema(Type type)
        {
            if (type == typeof(string))
            {
                return new Dictionary<string, object> { { "type", "string" } };
            }
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                return new Dictionary<string, object> { { "type", "integer" } };
            }
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                return new Dictionary<string, object> { { "type", "number" } };
            }
            if (type == typeof(bool))
            {
                return new Dictionary<string, object> { { "type", "boolean" } };
            }
            if (type == typeof(DateTime))
            {
                return new Dictionary<string, object> { { "type", "string" }, { "format", "date-time" } };
            }
            if (type.IsEnum)
            {
                var enumValues = new List<string>();
                foreach (var name in Enum.GetNames(type))
                {
                    enumValues.Add(name);
                }
                return new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "enum", enumValues }
                };
            }

            if (IsNullable(type))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                var schema = BuildSchema(underlyingType);
                var typeList = new List<object> { schema, new Dictionary<string, object> { { "type", "null" } } };
                return new Dictionary<string, object> { { "anyOf", typeList } };
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var itemSchema = BuildSchema(elementType);
                return new Dictionary<string, object>
                {
                    { "type", "array" },
                    { "items", itemSchema }
                };
            }

            if (typeof(IList).IsAssignableFrom(type) && type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    var itemSchema = BuildSchema(genericArgs[0]);
                    return new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "items", itemSchema }
                    };
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type) && type.IsGenericType)
            {
                return new Dictionary<string, object> { { "type", "object" } };
            }

            return BuildObjectSchema(type);
        }

        private static Dictionary<string, object> BuildObjectSchema(Type type)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            var props = type.GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (prop.GetGetMethod() == null)
                {
                    continue;
                }
                if (prop.GetSetMethod() == null)
                {
                    continue;
                }

                var propSchema = BuildSchema(prop.PropertyType);
                if (propSchema != null)
                {
                    var name = _naming.GetPropertyName(prop.Name, false);
                    properties[name] = propSchema;
                    if (!IsNullable(prop.PropertyType))
                    {
                        required.Add(name);
                    }
                }
            }

            if (properties.Count == 0)
            {
                return new Dictionary<string, object> { { "type", "object" } };
            }

            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "required", required },
                { "additionalProperties", false }
            };
        }

        private static bool IsNullable(Type type)
        {
            if (!type.IsValueType)
            {
                return false;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return true;
            }
            return false;
        }
    }
}