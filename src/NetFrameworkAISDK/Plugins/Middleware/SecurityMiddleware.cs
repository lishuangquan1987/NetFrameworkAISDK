using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NetFrameworkAISDK.Plugins.Middleware
{
    /// <summary>
    /// 安全中间件插件
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Middleware.SecurityMiddleware", "1.0.0")]
    [MiddlewarePlugin("Security")]
    public class SecurityMiddlewarePlugin : IMiddlewarePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Middleware.SecurityMiddleware"; } }
        public string Name { get { return "Security Middleware"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Filters content for security and privacy"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string MiddlewareType { get { return "Security"; } }

        private bool _enableContentFilter;
        private bool _enablePiiDetection;
        private List<string> _blockedPatterns;
        private Action<string> _securityLog;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _enableContentFilter = config.Settings.ContainsKey("enableContentFilter") 
                    ? Convert.ToBoolean(config.Settings["enableContentFilter"]) : true;
                _enablePiiDetection = config.Settings.ContainsKey("enablePiiDetection") 
                    ? Convert.ToBoolean(config.Settings["enablePiiDetection"]) : false;

                if (config.Settings.ContainsKey("blockedPatterns"))
                {
                    _blockedPatterns = config.Settings["blockedPatterns"] as List<string>;
                }
                else
                {
                    _blockedPatterns = new List<string>();
                }

                if (config.Settings.ContainsKey("securityLog"))
                {
                    _securityLog = config.Settings["securityLog"] as Action<string>;
                }
            }
            else
            {
                _enableContentFilter = true;
                _enablePiiDetection = false;
                _blockedPatterns = new List<string>();
            }
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IAgentMiddleware CreateMiddleware(PluginConfig config)
        {
            return new SecurityMiddleware(
                _enableContentFilter,
                _enablePiiDetection,
                _blockedPatterns,
                _securityLog);
        }
    }

    /// <summary>
    /// 安全中间件
    /// </summary>
    public class SecurityMiddleware : AgentMiddlewareBase
    {
        private readonly bool _enableContentFilter;
        private readonly bool _enablePiiDetection;
        private readonly List<Regex> _blockedPatterns;
        private readonly Action<string> _securityLog;

        private static readonly Regex EmailRegex = new Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new Regex(
            @"(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}",
            RegexOptions.Compiled);

        private static readonly Regex SsnRegex = new Regex(
            @"\d{3}-\d{2}-\d{4}",
            RegexOptions.Compiled);

        private static readonly Regex CreditCardRegex = new Regex(
            @"\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}",
            RegexOptions.Compiled);

        public SecurityMiddleware(
            bool enableContentFilter = true,
            bool enablePiiDetection = false,
            List<string> blockedPatterns = null,
            Action<string> securityLog = null)
        {
            _enableContentFilter = enableContentFilter;
            _enablePiiDetection = enablePiiDetection;
            _blockedPatterns = blockedPatterns != null 
                ? blockedPatterns.Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase)).ToList()
                : new List<Regex>();
            _securityLog = securityLog ?? (_ => { });
        }

        public override string Name
        {
            get { return "Security Middleware"; }
        }

        public override int Order
        {
            get { return -30; }
        }

        public override Common.ApiResponse<string> Invoke(
            AgentContext context,
            Func<Common.ApiResponse<string>> next)
        {
            if (_enableContentFilter && !string.IsNullOrEmpty(context.UserMessage))
            {
                var contentResult = CheckContent(context.UserMessage);
                if (contentResult != null)
                {
                    _securityLog("Blocked content: " + contentResult);
                    return new Common.ApiResponse<string>
                    {
                        Error = new Common.ApiError
                        {
                            Message = "Content blocked by security filter: " + contentResult
                        }
                    };
                }
            }

            var response = next();

            if (_enablePiiDetection && response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                var piiResult = DetectPii(response.Result);
                if (piiResult != null)
                {
                    _securityLog("PII detected in response: " + piiResult);
                    var sanitized = SanitizePii(response.Result, piiResult);
                    response = new Common.ApiResponse<string> { Result = sanitized };
                    context.SetItem("PiiDetected", true);
                    context.SetItem("PiiTypes", piiResult);
                }
            }

            return response;
        }

        private string CheckContent(string content)
        {
            foreach (var pattern in _blockedPatterns)
            {
                if (pattern.IsMatch(content))
                {
                    return "Content matches blocked pattern: " + pattern;
                }
            }

            return null;
        }

        private string DetectPii(string content)
        {
            var detectedTypes = new List<string>();

            if (EmailRegex.IsMatch(content))
                detectedTypes.Add("Email");
            if (PhoneRegex.IsMatch(content))
                detectedTypes.Add("Phone");
            if (SsnRegex.IsMatch(content))
                detectedTypes.Add("SSN");
            if (CreditCardRegex.IsMatch(content))
                detectedTypes.Add("CreditCard");

            return detectedTypes.Count > 0 ? string.Join(", ", detectedTypes) : null;
        }

        private string SanitizePii(string content, string piiTypes)
        {
            var result = content;

            result = EmailRegex.Replace(result, "[EMAIL]");
            result = PhoneRegex.Replace(result, "[PHONE]");
            result = SsnRegex.Replace(result, "[SSN]");
            result = CreditCardRegex.Replace(result, "[CREDIT_CARD]");

            return result;
        }
    }
}
