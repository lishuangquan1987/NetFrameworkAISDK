using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Plugins.Middleware
{
    /// <summary>
    /// 异常处理中间件插件
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Middleware.ExceptionHandlingMiddleware", "1.0.0")]
    [MiddlewarePlugin("ExceptionHandling")]
    public class ExceptionHandlingMiddlewarePlugin : IMiddlewarePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Middleware.ExceptionHandlingMiddleware"; } }
        public string Name { get { return "Exception Handling Middleware"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Handles exceptions and provides graceful error responses"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string MiddlewareType { get { return "ExceptionHandling"; } }

        private Action<string, Exception> _exceptionLogger;
        private int _maxRetries;
        private int _retryDelayMs;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _maxRetries = config.Settings.ContainsKey("maxRetries") 
                    ? Convert.ToInt32(config.Settings["maxRetries"]) : 0;
                _retryDelayMs = config.Settings.ContainsKey("retryDelayMs") 
                    ? Convert.ToInt32(config.Settings["retryDelayMs"]) : 1000;

                if (config.Settings.ContainsKey("exceptionLogger"))
                {
                    _exceptionLogger = config.Settings["exceptionLogger"] as Action<string, Exception>;
                }
            }
            else
            {
                _maxRetries = 0;
                _retryDelayMs = 1000;
            }
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IAgentMiddleware CreateMiddleware(PluginConfig config)
        {
            return new ExceptionHandlingMiddleware(_exceptionLogger, _maxRetries, _retryDelayMs);
        }
    }

    /// <summary>
    /// 异常处理中间件
    /// </summary>
    public class ExceptionHandlingMiddleware : AgentMiddlewareBase
    {
        private readonly Action<string, Exception> _exceptionLogger;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;

        public ExceptionHandlingMiddleware(
            Action<string, Exception> exceptionLogger,
            int maxRetries = 0,
            int retryDelayMs = 1000)
        {
            _exceptionLogger = exceptionLogger ?? (_, _) => { };
            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
        }

        public override string Name
        {
            get { return "Exception Handling Middleware"; }
        }

        public override int Order
        {
            get { return -10; }
        }

        public override Common.ApiResponse<string> Invoke(
            AgentContext context,
            Func<Common.ApiResponse<string>> next)
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts <= _maxRetries)
            {
                try
                {
                    return next();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;

                    _exceptionLogger(context.RequestId, ex);

                    if (attempts <= _maxRetries)
                    {
                        System.Threading.Thread.Sleep(_retryDelayMs);
                    }
                }
            }

            return new Common.ApiResponse<string>
            {
                Error = new Common.ApiError
                {
                    Message = "Request failed after " + attempts + " attempts. Last error: " + 
                             (lastException != null ? lastException.Message : "Unknown")
                }
            };
        }
    }
}
