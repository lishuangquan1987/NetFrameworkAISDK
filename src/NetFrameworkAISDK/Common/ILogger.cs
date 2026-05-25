namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 统一日志接口，用于 SDK 内部记录诊断信息、警告和错误。
    /// 调用方可通过自定义实现将日志接入任意日志框架（如 log4net、NLog 等）。
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录一条日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">
        /// 日志级别：&quot;DEBUG&quot;、&quot;INFO&quot;、&quot;WARN&quot;、&quot;ERROR&quot;。
        /// 默认实现仅对 WARN 和 ERROR 输出前缀
        /// </param>
        void Log(string message, string level);
    }
}
