using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NetFrameworkAISDK.Plugins.Tools
{
    /// <summary>
    /// Web 工具插件，提供网页抓取和搜索工具
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Tools.WebTools", "1.0.0")]
    [ToolProviderPlugin("Web")]
    public class WebToolsPlugin : IToolProviderPlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Tools.WebTools"; } }
        public string Name { get { return "Web Tools"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Provides web scraping and search tools"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string ToolCategory { get { return "Web"; } }

        private string _userAgent;
        private int _timeoutSeconds;
        private string _proxyUrl;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                _userAgent = config.Settings.ContainsKey("userAgent") 
                    ? config.Settings["userAgent"] as string 
                    : "Mozilla/5.0 (compatible; AI-Agent/1.0)";
                _timeoutSeconds = config.Settings.ContainsKey("timeoutSeconds") 
                    ? Convert.ToInt32(config.Settings["timeoutSeconds"]) 
                    : 30;
                _proxyUrl = config.Settings.ContainsKey("proxyUrl") 
                    ? config.Settings["proxyUrl"] as string 
                    : null;
            }
            else
            {
                _userAgent = "Mozilla/5.0 (compatible; AI-Agent/1.0)";
                _timeoutSeconds = 30;
            }
        }

        public PluginValidationResult Validate()
        {
            return PluginValidationResult.Success();
        }

        public IEnumerable<AIFunction> GetTools()
        {
            return new List<AIFunction>
            {
                CreateFetchPageTool(),
                CreateSearchWebTool(),
                CreateExtractLinksTool()
            };
        }

        public int GetToolCount()
        {
            return 3;
        }

        private AIFunction CreateFetchPageTool()
        {
            var method = typeof(WebToolsPlugin).GetMethod("FetchPage");
            return AIFunctionFactory.Create(method, this);
        }

        private AIFunction CreateSearchWebTool()
        {
            var method = typeof(WebToolsPlugin).GetMethod("SearchWeb");
            return AIFunctionFactory.Create(method, this);
        }

        private AIFunction CreateExtractLinksTool()
        {
            var method = typeof(WebToolsPlugin).GetMethod("ExtractLinks");
            return AIFunctionFactory.Create(method, this);
        }

        [Description("Fetch and extract text content from a web page")]
        public string FetchPage(
            [Description("URL of the web page to fetch")] string url,
            [Description("Maximum number of characters to return (0 for no limit)")] int maxLength = 5000)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "Error: URL is required.";
            }

            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = _userAgent;
                request.Timeout = _timeoutSeconds * 1000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                if (!string.IsNullOrEmpty(_proxyUrl))
                {
                    var proxy = new WebProxy(_proxyUrl);
                    request.Proxy = proxy;
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                {
                    var html = reader.ReadToEnd();
                    var text = ExtractTextFromHtml(html);

                    if (maxLength > 0 && text.Length > maxLength)
                    {
                        text = text.Substring(0, maxLength) + "... (truncated)";
                    }

                    var result = new Dictionary<string, object>
                    {
                        { "url", url },
                        { "title", ExtractTitle(html) },
                        { "content", text }
                    };

                    return JsonHelper.Serialize(result);
                }
            }
            catch (Exception ex)
            {
                return "Error fetching page: " + ex.Message;
            }
        }

        [Description("Search the web for information")]
        public string SearchWeb(
            [Description("Search query")] string query,
            [Description("Maximum number of results to return")] int maxResults = 5)
        {
            if (string.IsNullOrEmpty(query))
            {
                return "Error: Search query is required.";
            }

            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
                var searchUrl = "https://www.google.com/search?q=" + encodedQuery;

                var request = (HttpWebRequest)WebRequest.Create(searchUrl);
                request.UserAgent = _userAgent;
                request.Timeout = _timeoutSeconds * 1000;

                if (!string.IsNullOrEmpty(_proxyUrl))
                {
                    var proxy = new WebProxy(_proxyUrl);
                    request.Proxy = proxy;
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                {
                    var html = reader.ReadToEnd();
                    var links = ExtractSearchResults(html, maxResults);

                    var result = new Dictionary<string, object>
                    {
                        { "query", query },
                        { "results", links }
                    };

                    return JsonHelper.Serialize(result);
                }
            }
            catch (Exception ex)
            {
                return "Error searching web: " + ex.Message;
            }
        }

        [Description("Extract all links from a web page")]
        public string ExtractLinks(
            [Description("URL of the web page")] string url,
            [Description("Only return links containing this text (optional)")] string filter = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "Error: URL is required.";
            }

            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = _userAgent;
                request.Timeout = _timeoutSeconds * 1000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                {
                    var html = reader.ReadToEnd();
                    var baseUri = new Uri(url);
                    var links = ExtractAllLinks(html, baseUri);

                    if (!string.IsNullOrEmpty(filter))
                    {
                        links = links.FindAll(l => 
                            l.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    }

                    return JsonHelper.Serialize(links);
                }
            }
            catch (Exception ex)
            {
                return "Error extracting links: " + ex.Message;
            }
        }

        private static string ExtractTextFromHtml(string html)
        {
            var text = Regex.Replace(html, "<script[^>]*>.*?</script>", "", 
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<style[^>]*>.*?</style>", "", 
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private static string ExtractTitle(string html)
        {
            var match = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", 
                RegexOptions.IgnoreCase);
            return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : "";
        }

        private static List<Dictionary<string, string>> ExtractSearchResults(string html, int maxResults)
        {
            var results = new List<Dictionary<string, string>>();
            var linkPattern = new Regex(@"<a[^>]+href=""([^""]+)""[^>]*>([^<]+)</a>", 
                RegexOptions.IgnoreCase);
            var matches = linkPattern.Matches(html);

            foreach (Match match in matches)
            {
                if (results.Count >= maxResults)
                {
                    break;
                }

                var href = match.Groups[1].Value;
                var text = WebUtility.HtmlDecode(match.Groups[2].Value.Trim());

                if (href.StartsWith("/url?q="))
                {
                    href = href.Substring(8);
                    var ampIndex = href.IndexOf("&");
                    if (ampIndex > 0)
                    {
                        href = href.Substring(0, ampIndex);
                    }
                }

                if (!href.StartsWith("http") || text.Length < 10)
                {
                    continue;
                }

                results.Add(new Dictionary<string, string>
                {
                    { "url", href },
                    { "title", text }
                });
            }

            return results;
        }

        private static List<string> ExtractAllLinks(string html, Uri baseUri)
        {
            var links = new List<string>();
            var linkPattern = new Regex(@"<a[^>]+href=""([^""]+)""", RegexOptions.IgnoreCase);
            var matches = linkPattern.Matches(html);

            foreach (Match match in matches)
            {
                var href = match.Groups[1].Value;

                try
                {
                    if (href.StartsWith("//"))
                    {
                        href = "https:" + href;
                    }
                    else if (href.StartsWith("/"))
                    {
                        href = new Uri(baseUri, href).ToString();
                    }
                    else if (!href.StartsWith("http"))
                    {
                        href = new Uri(baseUri, href).ToString();
                    }

                    if (!links.Contains(href))
                    {
                        links.Add(href);
                    }
                }
                catch
                {
                }
            }

            return links;
        }
    }
}
