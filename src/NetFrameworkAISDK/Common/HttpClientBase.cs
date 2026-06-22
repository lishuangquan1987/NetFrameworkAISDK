using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// HTTP 客户端抽象基类，提供重试、超时和错误处理的基础设施。
    /// 配置 TLS 1.2 支持（兼容 .NET 4.0），提供 GET/POST/PUT/DELETE 方法。
    /// </summary>
    public abstract class HttpClientBase : IDisposable
    {
        // .NET 4.0 不包含 TLS 1.2 常量，使用数值定义
        // 192 = TLS 1.0
        // 768 = TLS 1.1
        // 3072 = TLS 1.2
        private const SecurityProtocolType Tls10 = (SecurityProtocolType)192;
        private const SecurityProtocolType Tls11 = (SecurityProtocolType)768;
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        // 48 = SSL 3.0（Windows XP 最终回退）
        private const SecurityProtocolType Ssl3 = (SecurityProtocolType)48;

        /// <summary>API 密钥</summary>
        protected readonly string ApiKey;

        /// <summary>API 基础 URL</summary>
        protected readonly string BaseUrl;

        /// <summary>从 BaseUrl 提取的 host:port（用于代理 X-Target-Host 头）</summary>
        private string BaseUrlHost
        {
            get
            {
                try
                {
                    var uri = new Uri(BaseUrl);
                    return uri.Host + ":" + uri.Port;
                }
                catch (UriFormatException ex)
                {
                    _defaultLogger.Log("HttpClientBase: Failed to parse BaseUrl '" + BaseUrl + "': " + ex.Message, "ERROR");
                    return null;
                }
            }
        }

        /// <summary>请求超时毫秒数</summary>
        protected readonly int TimeoutMilliseconds;

        /// <summary>日志记录器（实例级别，可通过构造函数注入）</summary>
        protected readonly ILogger _logger;

        private readonly int MaxRetries;
        private readonly int RetryDelayMilliseconds;

        /// <summary>默认日志记录器（静态初始化及回退使用）</summary>
        private static readonly ILogger _defaultLogger = new ConsoleLogger();

        /// <summary>TLS 1.2 是否被 OS 支持</summary>
        private static bool _tls12Supported;
        /// <summary>BouncyCastle 代理是否启用（XP 且不支持 TLS 1.2 时）</summary>
        private static bool _proxyEnabled;
        /// <summary>代理监听端口</summary>
        private static int _proxyPort;

        static HttpClientBase()
        {
            try
            {
                SecurityProtocolType supportedProtocols = 0;

                // 逐个探测操作系统支持的协议，从高到低尝试
                bool tls12Ok = false;
                try { ServicePointManager.SecurityProtocol = Tls12; supportedProtocols |= Tls12; tls12Ok = true; }
                catch (NotSupportedException) { }
                _tls12Supported = tls12Ok;

                try { ServicePointManager.SecurityProtocol = Tls11; supportedProtocols |= Tls11; }
                catch (NotSupportedException) { }

                try { ServicePointManager.SecurityProtocol = Tls10; supportedProtocols |= Tls10; }
                catch (NotSupportedException) { }

                try { ServicePointManager.SecurityProtocol = Ssl3; supportedProtocols |= Ssl3; }
                catch (NotSupportedException) { }

                if (supportedProtocols != 0)
                {
                    ServicePointManager.SecurityProtocol = supportedProtocols;
                    _defaultLogger.Log("HttpClientBase: Configured SecurityProtocol to " + supportedProtocols.ToString(), "DEBUG");
                }
                else
                {
                    _defaultLogger.Log("HttpClientBase: No supported SecurityProtocol found, using system default", "WARN");
                }
            }
            catch (Exception ex)
            {
                _defaultLogger.Log("HttpClientBase: Failed to configure SecurityProtocol: " + ex.Message, "ERROR");
            }

            ServicePointManager.DefaultConnectionLimit = 50;
            ServicePointManager.Expect100Continue = false;
            _defaultLogger.Log("HttpClientBase: Configured DefaultConnectionLimit=50, Expect100Continue=false", "DEBUG");

            // Windows XP 且 TLS 1.2 不被 OS 支持 → 启动 BouncyCastle 代理
            if (!_tls12Supported && IsWindowsXP())
            {
                try
                {
                    BouncyCastleTlsProxy.Instance.Start();
                    _proxyEnabled = true;
                    _proxyPort = BouncyCastleTlsProxy.Instance.Port;
                    _defaultLogger.Log("HttpClientBase: Started BouncyCastle TLS proxy on 127.0.0.1:" + _proxyPort, "INFO");
                }
                catch (Exception ex)
                {
                    _defaultLogger.Log("HttpClientBase: Failed to start TLS proxy: " + ex.Message, "ERROR");
                }
            }
        }

        /// <summary>检测是否为 Windows XP/Server 2003（版本号 5.x）</summary>
        private static bool IsWindowsXP()
        {
            return Environment.OSVersion.Version.Major == 5;
        }

        /// <summary>
        /// 【诊断用】强制启用 BouncyCastle TLS 代理，绕过 SChannel 直连。
        /// 在创建任何客户端之前调用；调用后所有 HTTPS 走本地代理。
        /// 非 XP 系统仅用于测试代理功能，生产环境无需调用。
        /// </summary>
        public static void ForceTlsProxyForDiagnostics()
        {
            try
            {
                BouncyCastleTlsProxy.Instance.Start();
                _proxyEnabled = true;
                _proxyPort = BouncyCastleTlsProxy.Instance.Port;
                _defaultLogger.Log("HttpClientBase: TLS proxy FORCE-ENABLED on 127.0.0.1:" + _proxyPort + " (diagnostic)", "WARN");
            }
            catch (Exception ex)
            {
                _defaultLogger.Log("HttpClientBase: Failed to start diagnostic TLS proxy: " + ex.Message, "ERROR");
            }
        }

        /// <summary>配置代理模式的请求参数（HTTP/1.0 + 新连接隔离 + X-Target-Host）</summary>
        private void ConfigureProxyRequest(HttpWebRequest request)
        {
            request.KeepAlive = false;
            request.ProtocolVersion = HttpVersion.Version10;
            request.ServicePoint.ConnectionLeaseTimeout = 0;
            request.ConnectionGroupName = Guid.NewGuid().ToString("N");
            string host = BaseUrlHost;
            if (!string.IsNullOrEmpty(host))
            {
                request.Headers["X-Target-Host"] = host;
            }
        }

        /// <summary>
        /// 创建 HTTP 客户端（默认 30 秒超时，2 次重试，1 秒延迟）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        protected HttpClientBase(string apiKey, string baseUrl)
            : this(apiKey, baseUrl, 30000, 2, 1000, null)
        {
        }

        /// <summary>
        /// 创建 HTTP 客户端（默认 30 秒超时，带日志）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="logger">日志记录器（可选，默认 ConsoleLogger）</param>
        protected HttpClientBase(string apiKey, string baseUrl, ILogger logger)
            : this(apiKey, baseUrl, 30000, 2, 1000, logger)
        {
        }

        /// <summary>
        /// 创建 HTTP 客户端（指定超时）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
            : this(apiKey, baseUrl, timeoutMilliseconds, 2, 1000, null)
        {
        }

        /// <summary>
        /// 创建 HTTP 客户端（完全自定义）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="retryDelayMilliseconds">重试延迟基数（毫秒）</param>
        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds, int maxRetries, int retryDelayMilliseconds)
            : this(apiKey, baseUrl, timeoutMilliseconds, maxRetries, retryDelayMilliseconds, null)
        {
        }

        /// <summary>
        /// 创建 HTTP 客户端（完全自定义 + 日志）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="retryDelayMilliseconds">重试延迟基数（毫秒）</param>
        /// <param name="logger">日志记录器（可选，默认 ConsoleLogger）</param>
        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds, int maxRetries, int retryDelayMilliseconds, ILogger logger)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("apiKey", "API key is required");
            }
            ApiKey = apiKey;
            BaseUrl = baseUrl.TrimEnd('/');
            TimeoutMilliseconds = timeoutMilliseconds;
            MaxRetries = maxRetries;
            RetryDelayMilliseconds = retryDelayMilliseconds;
            _logger = logger ?? _defaultLogger;
        }

        /// <summary>
        /// 配置 HTTP 请求的扩展点（子类可重写以添加自定义 Header 等）
        /// </summary>
        /// <param name="request">HTTP 请求对象</param>
        protected virtual void ConfigureRequest(HttpWebRequest request)
        {
        }

        /// <summary>
        /// 发送 GET 请求
        /// </summary>
        protected ApiResponse<T> Get<T>(string endpoint)
        {
            return Request<T>("GET", endpoint, null);
        }

        /// <summary>
        /// 发送带查询参数的 GET 请求
        /// </summary>
        protected ApiResponse<T> Get<T>(string endpoint, object queryParams)
        {
            return Request<T>("GET", endpoint, null, queryParams);
        }

        /// <summary>
        /// 发送 POST 请求
        /// </summary>
        protected ApiResponse<T> Post<T>(string endpoint, object data)
        {
            return Request<T>("POST", endpoint, data);
        }

        /// <summary>
        /// 发送 PUT 请求
        /// </summary>
        protected ApiResponse<T> Put<T>(string endpoint, object data)
        {
            return Request<T>("PUT", endpoint, data);
        }

        /// <summary>
        /// 发送 DELETE 请求
        /// </summary>
        protected ApiResponse<T> Delete<T>(string endpoint)
        {
            return Request<T>("DELETE", endpoint, null);
        }

        /// <summary>
        /// 发送带查询参数的 DELETE 请求
        /// </summary>
        protected ApiResponse<T> Delete<T>(string endpoint, object queryParams)
        {
            return Request<T>("DELETE", endpoint, null, queryParams);
        }

        /// <summary>
        /// 构建查询参数字符串
        /// </summary>
        private static string BuildQueryString(object queryParams)
        {
            // 直接处理 Dictionary<string, object>，避免双重序列化
            Dictionary<string, object> dict;
            if (queryParams is Dictionary<string, object>)
            {
                dict = (Dictionary<string, object>)queryParams;
            }
            else
            {
                // 对于匿名对象，仍需序列化-反序列化
                string json = JsonHelper.Serialize(queryParams);
                dict = JsonHelper.Deserialize<Dictionary<string, object>>(json);
            }
            
            var parts = new List<string>();
            foreach (var kvp in dict)
            {
                if (kvp.Value != null)
                {
                    parts.Add(kvp.Key + "=" + Uri.EscapeDataString(kvp.Value.ToString()));
                }
            }
            if (parts.Count == 0) { return ""; }
            return "?" + string.Join("&", parts);
        }

        /// <summary>
        /// 构建请求 URL（代理启用时改写为本地 HTTP 代理地址）
        /// </summary>
        private string BuildUrl(string endpoint)
        {
            if (_proxyEnabled)
            {
                var origBaseUri = new Uri(BaseUrl + "/");
                var targetUri = new Uri(origBaseUri, endpoint.TrimStart('/'));
                return "http://127.0.0.1:" + _proxyPort + targetUri.PathAndQuery;
            }

            var baseUri = new Uri(BaseUrl + "/");
            return new Uri(baseUri, endpoint.TrimStart('/')).ToString();
        }

        /// <summary>
        /// 构建带查询参数的 URL
        /// </summary>
        private string BuildUrlWithQuery(string endpoint, object queryParams)
        {
            if (_proxyEnabled)
            {
                var origBaseUri = new Uri(BaseUrl + "/");
                var targetUri = new Uri(origBaseUri, endpoint.TrimStart('/'));
                string query = queryParams != null ? BuildQueryString(queryParams) : "";
                string existing = targetUri.Query;
                if (!string.IsNullOrEmpty(existing))
                    query = string.IsNullOrEmpty(query) ? existing : existing + "&" + query.TrimStart('?');
                return "http://127.0.0.1:" + _proxyPort + targetUri.AbsolutePath + query;
            }
            var baseUri = new Uri(BaseUrl + "/");
            var uri = new Uri(baseUri, endpoint.TrimStart('/'));
            if (queryParams != null)
            {
                return uri.ToString() + BuildQueryString(queryParams);
            }
            return uri.ToString();
        }

        /// <summary>
        /// 执行 HTTP 请求（无查询参数入口）
        /// </summary>
        private ApiResponse<T> Request<T>(string method, string endpoint, object data, object queryParams = null)
        {
            return RequestWithRetry<T>(method, endpoint, data, queryParams, 0);
        }

        /// <summary>
        /// 执行 HTTP 请求并支持重试
        /// </summary>
        private ApiResponse<T> RequestWithRetry<T>(string method, string endpoint, object data, object queryParams, int attempt)
        {
            try
            {
                string url;
                if (queryParams != null)
                {
                    url = BuildUrlWithQuery(endpoint, queryParams);
                }
                else
                {
                    url = BuildUrl(endpoint);
                }
                
                _logger.Log(string.Format("HTTP {0} {1} (attempt {2})", method, url, attempt + 1), "DEBUG");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.ContentType = "application/json";
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                ConfigureRequest(request);
                if (_proxyEnabled)
                {
                    ConfigureProxyRequest(request);
                }

                if (data != null && (method == "POST" || method == "PUT"))
                {
                    string json = JsonHelper.Serialize(data);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    request.ContentLength = bytes.Length;
                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();
                    T result = JsonHelper.Deserialize<T>(content);
                    return new ApiResponse<T> { Result = result };
                }
            }
            catch (WebException ex)
            {
                if (ShouldRetry(ex, attempt))
                {
                    int delay = RetryDelayMilliseconds * (int)Math.Pow(2, attempt);
                    _logger.Log(string.Format("WebException, retrying in {0}ms...", delay), "WARN");
                    Thread.Sleep(delay);
                    return RequestWithRetry<T>(method, endpoint, data, queryParams, attempt + 1);
                }
                _logger.Log(string.Format("WebException, not retrying: {0}", ex.Message), "ERROR");
                return HandleWebException<T>(ex);
            }
            catch (Exception ex)
            {
                if (IsTransientException(ex) && attempt < MaxRetries)
                {
                    int delay = RetryDelayMilliseconds * (int)Math.Pow(2, attempt);
                    _logger.Log(string.Format("Transient exception, retrying in {0}ms: {1}", delay, ex.Message), "WARN");
                    Thread.Sleep(delay);
                    return RequestWithRetry<T>(method, endpoint, data, queryParams, attempt + 1);
                }
                _logger.Log(string.Format("Exception: {0}", ex.Message), "ERROR");
                return new ApiResponse<T> { Error = new ApiError(ex.Message) };
            }
        }

        /// <summary>
        /// 发送 POST 请求并处理 SSE 流式响应
        /// </summary>
        /// <typeparam name="T">每个 SSE 事件的反序列化类型</typeparam>
        /// <param name="endpoint">API 端点</param>
        /// <param name="data">请求体数据</param>
        /// <param name="onData">每收到一个数据块时的回调</param>
        /// <param name="onError">发生错误时的回调</param>
        protected void PostStream<T>(string endpoint, object data, Action<T> onData, Action<ApiError> onError)
        {
            string url = BuildUrl(endpoint);
            string json = JsonHelper.Serialize(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            int attempt = 0;
            while (true)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Timeout = TimeoutMilliseconds;
                    request.ReadWriteTimeout = TimeoutMilliseconds;
                    request.ContentLength = bytes.Length;
                    ConfigureRequest(request);
                    if (_proxyEnabled)
                    {
                        ConfigureProxyRequest(request);
                    }

                    using (Stream requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(bytes, 0, bytes.Length);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream == null)
                        {
                            onError(new ApiError("Empty response stream"));
                            return;
                        }
                        ParseSSEStream(responseStream, onData, onError);
                        return;
                    }
                }
                catch (WebException ex)
                {
                    // 已收到 HTTP 响应（非传输层错误）→ 不重试，直接返回
                    if (ex.Response != null)
                    {
                        ApiResponse<T> errorResponse = HandleWebException<T>(ex);
                        onError(errorResponse.Error);
                        return;
                    }

                    // 传输层错误 → 检查是否应重试
                    if (ShouldRetry(ex, attempt))
                    {
                        int delay = RetryDelayMilliseconds * (int)Math.Pow(2, attempt);
                        _logger.Log(string.Format("PostStream retry {0}/{1} after {2}ms: {3}", attempt + 1, MaxRetries, delay, ex.Message), "WARN");
                        Thread.Sleep(delay);
                        attempt++;
                        continue;
                    }

                    ApiResponse<T> errorResponse2 = HandleWebException<T>(ex);
                    onError(errorResponse2.Error);
                    return;
                }
                catch (Exception ex)
                {
                    onError(new ApiError(ex.Message));
                    return;
                }
            }
        }

        /// <summary>
        /// Parse SSE (Server-Sent Events) stream according to the specification.
        /// Events are separated by double newlines. Multiple data: lines are concatenated.
        /// </summary>
        private void ParseSSEStream<T>(Stream stream, Action<T> onData, Action<ApiError> onError)
        {
            StringBuilder eventData = new StringBuilder();
            byte[] buffer = new byte[4096];
            int bytesRead;
            string leftover = "";

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string chunk = leftover + Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] lines = chunk.Split('\n');

                leftover = lines[lines.Length - 1];

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string line = lines[i].TrimEnd('\r').Trim();

                    if (line.Length == 0)
                    {
                        if (eventData.Length > 0)
                        {
                            ProcessSSEEvent(eventData.ToString(), onData, onError);
                            eventData.Clear();
                        }
                    }
                    else if (line.StartsWith("data:"))
                    {
                        string data = line.Substring(5).TrimStart();
                        if (eventData.Length > 0)
                        {
                            eventData.Append('\n');
                        }
                        eventData.Append(data);
                    }
                }
            }

            if (!string.IsNullOrEmpty(leftover) && leftover.StartsWith("data:"))
            {
                string data = leftover.Substring(5).TrimStart().TrimEnd('\r');
                eventData.Append(data);
            }

            if (eventData.Length > 0)
            {
                ProcessSSEEvent(eventData.ToString(), onData, onError);
            }
        }

        /// <summary>
        /// Process a single SSE event, deserializing it to the target type.
        /// </summary>
        private void ProcessSSEEvent<T>(string eventData, Action<T> onData, Action<ApiError> onError)
        {
            if (string.IsNullOrWhiteSpace(eventData) || eventData == "[DONE]")
            {
                return;
            }

            try
            {
                T result = JsonHelper.Deserialize<T>(eventData);
                onData(result);
            }
            catch (Exception ex)
            {
                if (onError != null)
                {
                    onError(new ApiError("SSE parse error: " + ex.Message));
                }
                string truncated = eventData.Length > 100 ? eventData.Substring(0, 100) : eventData;
                _logger.Log("SSE parse error: " + ex.Message + " | Data: " + truncated, "WARN");
            }
        }

        /// <summary>
        /// 判断 WebException 是否应重试（传输层错误 / 429 / 5xx）
        /// </summary>
        private bool ShouldRetry(WebException ex, int attempt)
        {
            if (attempt >= MaxRetries)
            {
                return false;
            }

            var response = ex.Response as HttpWebResponse;
            if (response == null)
            {
                // 无 HTTP 响应 = 纯传输层错误（连接关闭、发送失败、超时等），应重试
                switch (ex.Status)
                {
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.SendFailure:
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.ReceiveFailure:
                    case WebExceptionStatus.Timeout:
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.KeepAliveFailure:
                        return true;
                    default:
                        return IsTransientException(ex);
                }
            }

            int statusCode = (int)response.StatusCode;
            return statusCode == 429 || statusCode >= 500;
        }

        /// <summary>
        /// 判断异常是否为瞬态错误（超时、连接中断等）
        /// </summary>
        private bool IsTransientException(Exception ex)
        {
            return ex is TimeoutException || ex is IOException;
        }

        /// <summary>
        /// 处理 WebException 并转换为 ApiResponse
        /// </summary>
        private static ApiResponse<T> HandleWebException<T>(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            int httpStatus = response != null ? (int)response.StatusCode : 0;
            string message = GetErrorMessage(response) ?? ex.Message;

            return new ApiResponse<T>
            {
                Error = new ApiError(message)
                {
                    Type = "api_error",
                    HttpStatusCode = httpStatus > 0 ? httpStatus : (int?)null
                }
            };
        }

        /// <summary>
        /// 从 HTTP 响应中提取错误消息
        /// </summary>
        private static string GetErrorMessage(HttpWebResponse response)
        {
            if (response == null)
            {
                return null;
            }
            try
            {
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null) { return null; }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}