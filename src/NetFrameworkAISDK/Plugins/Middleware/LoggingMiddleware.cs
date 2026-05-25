using System;
using System.Diagnostics;

namespace NetFrameworkAISDK.Plugins.Middleware
{
    /// <summary>
    /// 日志中间件插件，记录 Agent 执行过程
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Middleware.LoggingMiddleware", "1.0.0")]
    [MiddlewarePlugin("Logging")]
    public class LoggingMiddlewarePlugin : IMiddlewarePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Middleware.LoggingMiddleware"; } }
        public string Name { get { return "Logging Middleware"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Logs Agent execution details"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string MiddlewareType { get { return "Logging"; } }

        private Action<string> _logger;
        private bool _logRequest;
        private bool _logResponse;
        private bool _logToolCalls;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _logRequest = config.Settings.ContainsKey("logRequest") 
                    ? Convert.ToBoolean(config.Settings["logRequest"]) : true;
                _logResponse = config.Settings.ContainsKey("logResponse") 
                    ? Convert.ToBoolean(config.Settings["logResponse"]) : true;
                _logToolCalls = config.Settings.ContainsKey("logToolCalls") 
                    ? Convert.ToBoolean(config.Settings["logToolCalls"]) : true;
            }
            else
            {
                _logRequest = true;
                _logResponse = true;
                _logToolCalls = true;
            }

            _logger = config != null && config.Settings != null && config.Settings.ContainsKey("logger")
                ? config.Settings["logger"] as Action<string>
                : null;
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IAgentMiddleware CreateMiddleware(PluginConfig config)
        {
            return new LoggingMiddleware(_logger, _logRequest, _logResponse, _logToolCalls);
        }
    }

    /// <summary>
    /// 日志中间件
    /// </summary>
    public class LoggingMiddleware : LoggingMiddlewareBase
    {
        private readonly bool _logRequest;
        private readonly bool _logResponse;
        private readonly bool _logToolCalls;
        private readonly Stopwatch _stopwatch;

        public LoggingMiddleware(
            Action<string> logger,
            bool logRequest = true,
            bool logResponse = true,
            bool logToolCalls = true)
            : base(logger)
        {
            _logRequest = logRequest;
            _logResponse = logResponse;
            _logToolCalls = logToolCalls;
            _stopwatch = new Stopwatch();
        }

        public override string Name
        {
            get { return "Logging Middleware"; }
        }

        public override int Order
        {
            get { return 0; }
        }

        public override Common.ApiResponse<string> Invoke(
            AgentContext context,
            Func<Common.ApiResponse<string>> next)
        {
            _stopwatch.Restart();

            if (_logRequest)
            {
                LogInfo("[" + context.RequestId + "] Request started: " + context.UserMessage);
            }

            Common.ApiResponse<string> response = null;
            Exception caughtException = null;

            try
            {
                response = next();
            }
            catch (Exception ex)
            {
                caughtException = ex;
                LogError("[" + context.RequestId + "] Exception: " + ex.Message);
            }

            _stopwatch.Stop();

            if (_logResponse && caughtException == null)
            {
                if (response != null && response.IsSuccess)
                {
                    var preview = response.Result != null && response.Result.Length > 100
                        ? response.Result.Substring(0, 100) + "..."
                        : response.Result;
                    LogInfo("[" + context.RequestId + "] Response completed in " + 
                           _stopwatch.ElapsedMilliseconds + "ms: " + preview);
                }
                else if (response != null)
                {
                    LogWarning("[" + context.RequestId + "] Response failed: " + 
                              response.Error != null ? response.Error.Message : "Unknown error");
                }
            }

            return response ?? new Common.ApiResponse<string>
            {
                Error = new Common.ApiError 
                { 
                    Message = caughtException != null 
                        ? caughtException.Message 
                        : "Unknown error occurred" 
                }
            };
        }
    }
}
