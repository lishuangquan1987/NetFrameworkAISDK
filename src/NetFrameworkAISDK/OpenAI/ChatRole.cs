namespace NetFrameworkAISDK.OpenAI
{
    /// <summary>
    /// OpenAI 聊天消息角色常量
    /// </summary>
    public static class ChatRole
    {
        /// <summary>系统指令角色</summary>
        public const string System = "system";

        /// <summary>用户消息角色</summary>
        public const string User = "user";

        /// <summary>AI 助手消息角色</summary>
        public const string Assistant = "assistant";

        /// <summary>工具执行结果角色</summary>
        public const string Tool = "tool";
    }
}