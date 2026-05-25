using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetFrameworkAISDK.Plugins.Middleware
{
    /// <summary>
    /// 缓存中间件插件
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Middleware.CachingMiddleware", "1.0.0")]
    [MiddlewarePlugin("Caching")]
    public class CachingMiddlewarePlugin : IMiddlewarePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Middleware.CachingMiddleware"; } }
        public string Name { get { return "Caching Middleware"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Caches agent responses for identical requests"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string MiddlewareType { get { return "Caching"; } }

        private int _cacheExpirationMinutes;
        private int _maxCacheSize;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _cacheExpirationMinutes = config.Settings.ContainsKey("cacheExpirationMinutes") 
                    ? Convert.ToInt32(config.Settings["cacheExpirationMinutes"]) : 30;
                _maxCacheSize = config.Settings.ContainsKey("maxCacheSize") 
                    ? Convert.ToInt32(config.Settings["maxCacheSize"]) : 1000;
            }
            else
            {
                _cacheExpirationMinutes = 30;
                _maxCacheSize = 1000;
            }
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IAgentMiddleware CreateMiddleware(PluginConfig config)
        {
            return new CachingMiddleware(_cacheExpirationMinutes, _maxCacheSize);
        }
    }

    /// <summary>
    /// 缓存中间件
    /// </summary>
    public class CachingMiddleware : AgentMiddlewareBase
    {
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly int _cacheExpirationMinutes;
        private readonly int _maxCacheSize;
        private readonly object _lock = new object();
        private readonly SHA256 _sha256;

        public CachingMiddleware(int cacheExpirationMinutes = 30, int maxCacheSize = 1000)
        {
            _cacheExpirationMinutes = cacheExpirationMinutes;
            _maxCacheSize = maxCacheSize;
            _cache = new Dictionary<string, CacheEntry>();
            _sha256 = SHA256.Create();
        }

        public override string Name
        {
            get { return "Caching Middleware"; }
        }

        public override int Order
        {
            get { return 10; }
        }

        public override Common.ApiResponse<string> Invoke(
            AgentContext context,
            Func<Common.ApiResponse<string>> next)
        {
            var cacheKey = GenerateCacheKey(context.UserMessage, context.ContentParts);

            lock (_lock)
            {
                CacheEntry entry;
                if (_cache.TryGetValue(cacheKey, out entry))
                {
                    if (entry.CreatedAt.AddMinutes(_cacheExpirationMinutes) > DateTime.UtcNow)
                    {
                        context.SetItem("CacheHit", true);
                        context.SetItem("CacheKey", cacheKey);
                        return new Common.ApiResponse<string> { Result = entry.Response };
                    }
                    else
                    {
                        _cache.Remove(cacheKey);
                    }
                }
            }

            context.SetItem("CacheHit", false);
            context.SetItem("CacheKey", cacheKey);

            var response = next();

            if (response.IsSuccess && !string.IsNullOrEmpty(response.Result))
            {
                lock (_lock)
                {
                    if (_cache.Count >= _maxCacheSize)
                    {
                        RemoveOldestEntry();
                    }

                    _cache[cacheKey] = new CacheEntry
                    {
                        Response = response.Result,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }

            return response;
        }

        private string GenerateCacheKey(string message, List<Common.MessageContent> contentParts)
        {
            var sb = new StringBuilder();
            sb.Append(message);

            if (contentParts != null)
            {
                foreach (var part in contentParts)
                {
                    sb.Append("|");
                    sb.Append(part.Type);
                    if (part.Text != null)
                    {
                        sb.Append(":");
                        sb.Append(part.Text);
                    }
                }
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = _sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private void RemoveOldestEntry()
        {
            string oldestKey = null;
            DateTime oldestTime = DateTime.MaxValue;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.CreatedAt < oldestTime)
                {
                    oldestTime = kvp.Value.CreatedAt;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                _cache.Remove(oldestKey);
            }
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        public int GetCacheSize()
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }

        private class CacheEntry
        {
            public string Response { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
