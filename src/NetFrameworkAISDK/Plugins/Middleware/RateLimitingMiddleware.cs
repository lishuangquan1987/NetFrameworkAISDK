using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Plugins.Middleware
{
    /// <summary>
    /// 限流中间件插件
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Middleware.RateLimitingMiddleware", "1.0.0")]
    [MiddlewarePlugin("RateLimiting")]
    public class RateLimitingMiddlewarePlugin : IMiddlewarePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Middleware.RateLimitingMiddleware"; } }
        public string Name { get { return "Rate Limiting Middleware"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Limits request rate to prevent API overload"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string MiddlewareType { get { return "RateLimiting"; } }

        private int _requestsPerMinute;
        private int _requestsPerHour;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _requestsPerMinute = config.Settings.ContainsKey("requestsPerMinute") 
                    ? Convert.ToInt32(config.Settings["requestsPerMinute"]) : 60;
                _requestsPerHour = config.Settings.ContainsKey("requestsPerHour") 
                    ? Convert.ToInt32(config.Settings["requestsPerHour"]) : 1000;
            }
            else
            {
                _requestsPerMinute = 60;
                _requestsPerHour = 1000;
            }
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IAgentMiddleware CreateMiddleware(PluginConfig config)
        {
            return new RateLimitingMiddleware(_requestsPerMinute, _requestsPerHour);
        }
    }

    /// <summary>
    /// 限流中间件
    /// </summary>
    public class RateLimitingMiddleware : AgentMiddlewareBase
    {
        private readonly int _requestsPerMinute;
        private readonly int _requestsPerHour;
        private readonly Queue<DateTime> _minuteRequests;
        private readonly Queue<DateTime> _hourRequests;
        private readonly object _lock = new object();

        public RateLimitingMiddleware(int requestsPerMinute = 60, int requestsPerHour = 1000)
        {
            _requestsPerMinute = requestsPerMinute;
            _requestsPerHour = requestsPerHour;
            _minuteRequests = new Queue<DateTime>();
            _hourRequests = new Queue<DateTime>();
        }

        public override string Name
        {
            get { return "Rate Limiting Middleware"; }
        }

        public override int Order
        {
            get { return -20; }
        }

        public override Common.ApiResponse<string> Invoke(
            AgentContext context,
            Func<Common.ApiResponse<string>> next)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                CleanupOldRequests(now);

                if (_minuteRequests.Count >= _requestsPerMinute)
                {
                    var waitTime = (int)(_minuteRequests.Peek().AddMinutes(1) - now).TotalSeconds;
                    waitTime = Math.Max(1, waitTime);

                    return new Common.ApiResponse<string>
                    {
                        Error = new Common.ApiError
                        {
                            Message = "Rate limit exceeded. Please wait " + waitTime + " seconds. " +
                                     "Limit: " + _requestsPerMinute + " requests per minute."
                        }
                    };
                }

                if (_hourRequests.Count >= _requestsPerHour)
                {
                    var waitTime = (int)(_hourRequests.Peek().AddHours(1) - now).TotalSeconds;
                    waitTime = Math.Max(1, waitTime);

                    return new Common.ApiResponse<string>
                    {
                        Error = new Common.ApiError
                        {
                            Message = "Hourly rate limit exceeded. Please wait " + waitTime + " seconds. " +
                                     "Limit: " + _requestsPerHour + " requests per hour."
                        }
                    };
                }

                _minuteRequests.Enqueue(now);
                _hourRequests.Enqueue(now);
            }

            return next();
        }

        private void CleanupOldRequests(DateTime now)
        {
            while (_minuteRequests.Count > 0 && 
                   _minuteRequests.Peek().AddMinutes(1) < now)
            {
                _minuteRequests.Dequeue();
            }

            while (_hourRequests.Count > 0 && 
                   _hourRequests.Peek().AddHours(1) < now)
            {
                _hourRequests.Dequeue();
            }
        }

        public int GetCurrentMinuteCount()
        {
            lock (_lock)
            {
                return _minuteRequests.Count;
            }
        }

        public int GetCurrentHourCount()
        {
            lock (_lock)
            {
                return _hourRequests.Count;
            }
        }
    }
}
