using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    public abstract class HttpClientBase : IDisposable
    {
        protected readonly string ApiKey;
        protected readonly string BaseUrl;
        protected readonly int TimeoutMilliseconds;
        private readonly int MaxRetries;
        private readonly int RetryDelayMilliseconds;

        static HttpClientBase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HttpClientBase: Failed to configure SecurityProtocol: " + ex.Message);
            }
        }

        protected HttpClientBase(string apiKey, string baseUrl)
            : this(apiKey, baseUrl, 30000, 2, 1000)
        {
        }

        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
            : this(apiKey, baseUrl, timeoutMilliseconds, 2, 1000)
        {
        }

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

        protected virtual void ConfigureRequest(HttpWebRequest request)
        {
        }

        protected ApiResponse<T> Get<T>(string endpoint)
        {
            return Request<T>("GET", endpoint, null);
        }

        protected ApiResponse<T> Get<T>(string endpoint, object queryParams)
        {
            return Request<T>("GET", endpoint, null, queryParams);
        }

        protected ApiResponse<T> Post<T>(string endpoint, object data)
        {
            return Request<T>("POST", endpoint, data);
        }

        protected ApiResponse<T> Put<T>(string endpoint, object data)
        {
            return Request<T>("PUT", endpoint, data);
        }

        protected ApiResponse<T> Delete<T>(string endpoint)
        {
            return Request<T>("DELETE", endpoint, null);
        }

        protected ApiResponse<T> Delete<T>(string endpoint, object queryParams)
        {
            return Request<T>("DELETE", endpoint, null, queryParams);
        }

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

        private string BuildUrl(string endpoint)
        {
            var baseUri = new Uri(BaseUrl + "/");
            return new Uri(baseUri, endpoint.TrimStart('/')).ToString();
        }

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

        private ApiResponse<T> Request<T>(string method, string endpoint, object data, object queryParams = null)
        {
            return RequestWithRetry<T>(method, endpoint, data, queryParams, 0);
        }

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
                    Thread.Sleep(RetryDelayMilliseconds * (attempt + 1));
                    return RequestWithRetry<T>(method, endpoint, data, queryParams, attempt + 1);
                }
                return HandleWebException<T>(ex);
            }
            catch (Exception ex)
            {
                if (IsTransientException(ex) && attempt < MaxRetries)
                {
                    Thread.Sleep(RetryDelayMilliseconds * (attempt + 1));
                    return RequestWithRetry<T>(method, endpoint, data, queryParams, attempt + 1);
                }
                return new ApiResponse<T> { Error = new ApiError(ex.Message) };
            }
        }

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
                            if (line.StartsWith("data:"))
                            {
                                string dataLine = line.Substring(5).TrimStart();
                                if (dataLine == "[DONE]")
                                {
                                    break;
                                }
                                try
                                {
                                    T result = JsonHelper.Deserialize<T>(dataLine);
                                    onData(result);
                                }
                                catch (Exception parseEx)
                                {
                                    Debug.WriteLine("SSE parse error: " + parseEx.Message);
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

        private ApiResponse<T> HandleWebException<T>(WebException ex)
        {
            string errorMessage = ex.Message;
            int? httpStatusCode = null;

            if (ex.Response != null)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                if (httpResponse != null)
                {
                    httpStatusCode = (int)httpResponse.StatusCode;
                }

                using (Stream stream = ex.Response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string errorContent = reader.ReadToEnd();
                    try
                    {
                        ApiError error = JsonHelper.Deserialize<ApiError>(errorContent);
                        if (!string.IsNullOrEmpty(error.Message))
                        {
                            error.HttpStatusCode = httpStatusCode;
                            return new ApiResponse<T> { Error = error };
                        }
                    }
                    catch
                    {
                        errorMessage = errorContent;
                    }
                }
            }

            return new ApiResponse<T>
            {
                Error = new ApiError(errorMessage)
                {
                    HttpStatusCode = httpStatusCode
                }
            };
        }

        private bool ShouldRetry(WebException ex, int attempt)
        {
            if (attempt >= MaxRetries)
            {
                return false;
            }

            var httpResponse = ex.Response as HttpWebResponse;
            if (httpResponse != null)
            {
                int statusCode = (int)httpResponse.StatusCode;
                if (statusCode == 429)
                {
                    return true;
                }
                if (statusCode >= 500 && statusCode < 600)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTransientException(Exception ex)
        {
            if (ex is TimeoutException)
            {
                return true;
            }
            if (ex is IOException && ex.InnerException != null && ex.InnerException is System.Net.Sockets.SocketException)
            {
                return true;
            }
            return false;
        }

        public virtual void Dispose()
        {
        }
    }
}