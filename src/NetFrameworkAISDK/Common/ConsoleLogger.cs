using System.Diagnostics;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// ILogger 的默认实现，将日志消息输出到 <see cref="Debug.WriteLine"/>。
    /// WARN 和 ERROR 级别自动添加前缀 &quot;[WARN]&quot; / &quot;[ERROR]&quot;。
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        /// <inheritdoc />
        public void Log(string message, string level)
        {
            if (message == null)
            {
                return;
            }

            if (level == "WARN" || level == "ERROR")
            {
                Debug.WriteLine("[" + level + "] " + message);
            }
            else
            {
                Debug.WriteLine(message);
            }
        }
    }
}
