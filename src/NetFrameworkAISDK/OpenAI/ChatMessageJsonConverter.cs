using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// ChatMessage 自定义 JSON 转换器，处理 content 字段的多态性：
    /// 纯文本时 content 为字符串，多模态时 content 为 ImageContentPart 数组。
    /// 解决 Content（string）和 ContentParts（List&lt;ImageContentPart&gt;）同时映射到
    /// JSON "content" 字段导致的反序列化冲突。
    /// </summary>
    public class ChatMessageJsonConverter : JsonConverter<ChatMessage>
    {
        /// <inheritdoc />
        public override ChatMessage ReadJson(JsonReader reader, Type objectType, ChatMessage existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject obj = JObject.Load(reader);
            var msg = new ChatMessage();

            // role
            JToken roleToken;
            if (obj.TryGetValue("role", out roleToken) && roleToken.Type != JTokenType.Null)
                msg.Role = roleToken.Value<string>();

            // content — polymorphic: string (纯文本) or array (多模态)
            JToken contentToken;
            if (obj.TryGetValue("content", out contentToken) && contentToken.Type != JTokenType.Null)
            {
                if (contentToken.Type == JTokenType.String)
                {
                    msg.Content = contentToken.Value<string>();
                }
                else if (contentToken.Type == JTokenType.Array)
                {
                    msg.ContentParts = contentToken.ToObject<List<ImageContentPart>>(serializer);
                }
            }

            // name
            JToken nameToken;
            if (obj.TryGetValue("name", out nameToken) && nameToken.Type != JTokenType.Null)
                msg.Name = nameToken.Value<string>();

            // tool_call_id
            JToken tcidToken;
            if (obj.TryGetValue("tool_call_id", out tcidToken) && tcidToken.Type != JTokenType.Null)
                msg.ToolCallId = tcidToken.Value<string>();

            // tool_calls
            JToken tcToken;
            if (obj.TryGetValue("tool_calls", out tcToken) && tcToken.Type == JTokenType.Array)
                msg.ToolCalls = tcToken.ToObject<List<ToolCall>>(serializer);

            // reasoning_content
            JToken rcToken;
            if (obj.TryGetValue("reasoning_content", out rcToken) && rcToken.Type != JTokenType.Null)
                msg.ReasoningContent = rcToken.Value<string>();

            return msg;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, ChatMessage value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            JObject obj = new JObject();

            // role
            if (!string.IsNullOrEmpty(value.Role))
                obj["role"] = value.Role;

            // content — ContentParts 优先（多模态数组），否则用 Content（纯文本字符串）
            if (value.ContentParts != null && value.ContentParts.Count > 0)
            {
                obj["content"] = JToken.FromObject(value.ContentParts, serializer);
            }
            else if (!string.IsNullOrEmpty(value.Content))
            {
                obj["content"] = value.Content;
            }

            // name
            if (!string.IsNullOrEmpty(value.Name))
                obj["name"] = value.Name;

            // tool_call_id
            if (!string.IsNullOrEmpty(value.ToolCallId))
                obj["tool_call_id"] = value.ToolCallId;

            // tool_calls
            if (value.ToolCalls != null && value.ToolCalls.Count > 0)
                obj["tool_calls"] = JToken.FromObject(value.ToolCalls, serializer);

            // reasoning_content
            if (!string.IsNullOrEmpty(value.ReasoningContent))
                obj["reasoning_content"] = value.ReasoningContent;

            obj.WriteTo(writer);
        }
    }
}
