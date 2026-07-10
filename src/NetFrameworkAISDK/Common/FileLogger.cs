using System;
using System.IO;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// 默认文件日志记录器。写入 BaseDir/Logs/NetFrameworkSDK-{yyyy-MM-dd}.log。
    /// 日志目录自动创建，线程安全。
    /// </summary>
    public class FileLogger : ILogger
    {
        private static readonly object _lock = new object();
        private readonly string _filePath;

        /// <summary>
        /// 创建 FileLogger，日志路径为 Logs/NetFrameworkSDK-{yyyy-MM-dd}.log
        /// </summary>
        /// <param name="logDir">日志目录，相对于 BaseDirectory；默认 "Logs"</param>
        public FileLogger(string logDir = "Logs")
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fullDir = Path.Combine(baseDir, logDir);
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }
            string fileName = string.Format("NetFrameworkSDK-{0}.log", DateTime.Now.ToString("yyyy-MM-dd"));
            _filePath = Path.Combine(fullDir, fileName);
        }

        /// <summary>
        /// 记录日志。线程安全，追加写入。
        /// 格式: [yyyy-MM-dd HH:mm:ss] [LEVEL] message
        /// </summary>
        public void Log(string message, string level)
        {
            if (message == null) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string line = string.Format("[{0}] [{1}] {2}", timestamp, level, message);

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
