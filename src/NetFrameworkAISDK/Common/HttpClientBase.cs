using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace NetFrameworkAISDK.Common
{
    public abstract class HttpClientBase : IDisposable
    {
        protected readonly string ApiKey;
        protected readonly string BaseUrl;
        protected readonly int TimeoutMilliseconds;

        static HttpClientBase()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
            }
            catch
            {
            }
        }

        protected HttpClientBase(string apiKey, string baseUrl)
            : this(apiKey, baseUrl, 30000)
        {
        }

        protected HttpClientBase(string apiKey, string baseUrl, int timeoutMilliseconds)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("apiKey", "API key is required");
            }
            ApiKey = apiKey;
            BaseUrl = baseUrl.TrimEnd('/');
            TimeoutMilliseconds = timeoutMilliseconds;
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
            var properties = queryParams.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var parts = new List<string>();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(queryParams, null);
                if (value != null)
                {
                    parts.Add(prop.Name + "=" + Uri.EscapeDataString(value.ToString()));
                }
            }
            if (parts.Count == 0) { return ""; }
            return "?" + string.Join("&", parts);
        }

        private ApiResponse<T> Request<T>(string method, string endpoint, object data, object queryParams = null)
        {
            try
            {
                string url = BaseUrl + "/" + endpoint.TrimStart('/');
                if (queryParams != null)
                {
                    url = url + BuildQueryString(queryParams);
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
                return HandleWebException<T>(ex);
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Error = new ApiError(ex.Message) };
            }
        }

        protected void PostStream<T>(string endpoint, object data, Action<T> onData, Action<ApiError> onError)
        {
            try
            {
                string url = BaseUrl + "/" + endpoint.TrimStart('/');

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
                            if (line.Length > 6 && line.StartsWith("data: "))
                            {
                                string dataLine = line.Substring(6);
                                if (dataLine == "[DONE]")
                                {
                                    break;
                                }
                                try
                                {
                                    T result = JsonHelper.Deserialize<T>(dataLine);
                                    onData(result);
                                }
                                catch
                                {
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

            if (ex.Response != null)
            {
                using (Stream stream = ex.Response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string errorContent = reader.ReadToEnd();
                    try
                    {
                        ApiError error = JsonHelper.Deserialize<ApiError>(errorContent);
                        if (!string.IsNullOrEmpty(error.Message))
                        {
                            return new ApiResponse<T> { Error = error };
                        }
                    }
                    catch
                    {
                        errorMessage = errorContent;
                    }
                }
            }

            return new ApiResponse<T> { Error = new ApiError(errorMessage) };
        }

        public virtual void Dispose()
        {
        }
    }
}
