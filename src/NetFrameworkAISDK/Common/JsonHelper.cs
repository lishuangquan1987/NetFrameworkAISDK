using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// JSON 序列化辅助类，使用 snake_case 命名策略与 OpenAI/Anthropic API 兼容。
    /// 所有 JSON 序列化和反序列化均通过此类进行，确保字段名正确映射。
    /// </summary>
    internal static class JsonHelper
    {
        private static readonly JsonSerializerSettings _settings;

        static JsonHelper()
        {
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                },
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串（snake_case 命名）
        /// </summary>
        /// <param name="obj">要序列化的对象</param>
        /// <returns>JSON 字符串</returns>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定类型（snake_case 命名）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="json">JSON 字符串</param>
        /// <returns>反序列化后的对象</returns>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定运行时类型（snake_case 命名）
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="type">运行时类型</param>
        /// <returns>反序列化后的对象</returns>
        public static object Deserialize(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, _settings);
        }
    }
}