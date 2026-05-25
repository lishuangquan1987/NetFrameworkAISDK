using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>API 密钥</summary>
        protected readonly string ApiKey;

        /// <summary>API 基础 URL</summary>
        protected readonly string BaseUrl;

        /// <summary>请求超时毫秒数</summary>
        protected readonly int TimeoutMilliseconds;

        private readonly int MaxRetries;
        private readonly int RetryDelayMilliseconds;
        private static readonly ILogger _logger = new ConsoleLogger();

        static HttpClientBase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = Tls12 | Tls11 | Tls10;
                _logger.Log("HttpClientBase: Configured SecurityProtocol to TLS 1.2, 1.1, and 1.0", "DEBUG");
            }
            catch (Exception ex)
            {
                _logger.Log("HttpClientBase: Failed to configure SecurityProtocol: " + ex.Message, "ERROR");
            }
        }

        /// <summary>
        /// 创建 HTTP 客户端（默认 30 秒超时，2 次重试，1 秒延迟）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        protected HttpClientBase(string apiKey, string baseUrl)
            : this(apiKey, baseUrl, 30000, 2, 1000)
        {
        }

        /// <summary>
        /// 创建 HTTP 客户端（指定超时）
        /// </summary>
        /// <param name="apiKey">API 密钥</param>
        /// <param name="baseUrl">API 基础 URL</param>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
            : this(apiKey, baseUrl, timeoutMilliseconds, 2, 1000)
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
            string json = JsonHelper.Serialize(queryParams);
            var dict = JsonHelper.Deserialize<Dictionary<string, object>>(json);
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
        /// 构建请求 URL
        /// </summary>
        private string BuildUrl(string endpoint)
        {
            var baseUri = new Uri(BaseUrl + "/");
            return new Uri(baseUri, endpoint.TrimStart('/')).ToString();
        }

        /// <summary>
        /// 构建带查询参数的 URL
        /// </summary>
        private string BuildUrlWithQuery(string endpoint, object queryParams)
        {
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
            try
            {
                string url = BuildUrl(endpoint);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                ConfigureRequest(request);

                string json = JsonHelper.Serialize(data);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null)
                    {
                        onError(new ApiError("Empty response stream"));
                        return;
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            
                            // 忽略空行和非 data: 开头的行（event:、id: 等）
                            if (string.IsNullOrEmpty(line))
                            {
                                continue;
                            }
                            
                            if (line.StartsWith("data:"))
                            {
                                // 安全提取 data: 后的内容
                                int colonIndex = line.IndexOf(':');
                                if (colonIndex < 0)
                                {
                                    continue;
                                }
                                string dataLine = line.Substring(colonIndex + 1).TrimStart();
                                
                                if (dataLine == "[DONE]")
                                {
                                    break;
                                }
                                
                                // 忽略空数据
                                if (string.IsNullOrWhiteSpace(dataLine))
                                {
                                    continue;
                                }
                                
                                try
                                {
                                    T result = JsonHelper.Deserialize<T>(dataLine);
                                    onData(result);
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.Log("SSE parse error: " + parseEx.Message, "WARN");
                                }
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                ApiResponse<T> errorResponse = HandleWebException<T>(ex);
                onError(errorResponse.Error);
            }
            catch (Exception ex)
            {
                onError(new ApiError(ex.Message));
            }
        }

        /// <summary>
        /// 判断 WebException 是否应重试（429/5xx 状态码）
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
                return IsTransientException(ex);
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