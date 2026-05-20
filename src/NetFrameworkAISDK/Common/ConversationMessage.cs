using System.Collections.Generic;

namespace NetFrameworkAISDK.Common
{
    public static class MessageRole
    {
        public const string System = "system";
        public const string User = "user";
        public const string Assistant = "assistant";
        public const string Tool = "tool";
    }

    public class ConversationMessage
    {
        public string Role { get; set; }

        public string Content { get; set; }

        public string Name { get; set; }

        public string ToolCallId { get; set; }

        public List<ToolCallRequest> ToolCalls { get; set; }
    }

    public class ToolCallRequest
    {
        public string Id { get; set; }

        public string Type { get; set; }

        public string FunctionName { get; set; }

        public string FunctionArguments { get; set; }
    }

    public class ConversationOptions
    {
        public string Model { get; set; }

        public string SystemPrompt { get; set; }

        public int? MaxTokens { get; set; }

        public double? Temperature { get; set; }

        public List<AIFunction> Tools { get; set; }

        public bool Stream { get; set; }
    }

    public class ConversationResponse
    {
        public string Content { get; set; }

        public string Model { get; set; }

        public List<ToolCallRequest> ToolCalls { get; set; }

        public string FinishReason { get; set; }
    }
}