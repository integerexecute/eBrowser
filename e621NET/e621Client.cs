using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Web;
using HtmlAgilityPack;

using e621NET.Data.Posts;

namespace e621NET
{
    public class e621Client : IDisposable
    {
        private readonly HttpClient _http;
        private bool _disposed = false;

        public bool DebugMode { get; set; }

        public string Host { get; }
        public e621ClientOptions Options { get; }

        private const int POSTS_HARD_LIMIT = 320;

        public e621Client(e621ClientOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Host = string.IsNullOrWhiteSpace(options.HostUri) ? "https://e621.net/" : options.HostUri;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _http = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 100)
            };

            // Note: We do not mutate DefaultRequestHeaders globally.
        }

        #region Public API

        /// <summary>
        /// Sets credentials to be used on subsequent requests.
        /// This does not mutate global HttpClient headers; the header is applied per-request.
        /// </summary>
        public void SetCredentials(e621APICredentials credentials)
        {
            Options.Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        /// <summary>
        /// Get posts via JSON API. If fetchMaxPage is true, the client will also request the HTML page
        /// and attempt to determine the site's maximum page (fragile - optional).
        /// </summary>
        public async Task<ePosts?> GetPostsAsync(string? tags = null, int page = 1, int limit = -1, bool fetchMaxPage = false)
        {
            if (limit > POSTS_HARD_LIMIT)
                throw new e621ClientException(ClientErrorType.Internal, $"The posts maximum limit is {POSTS_HARD_LIMIT}");

            var q = HttpUtility.ParseQueryString(string.Empty);
            if (!string.IsNullOrWhiteSpace(tags)) q["tags"] = tags;
            if (page > 1) q["page"] = page.ToString();
            if (limit > 0) q["limit"] = limit.ToString();

            var jsonUrl = GetUri(Host, "posts.json") + (q.Count > 0 ? ("?" + q) : string.Empty);
            var htmlUrl = GetUri(Host, "posts") + (q.Count > 0 ? ("?" + q) : string.Empty);

            // Request JSON
            var jsonReq = BuildRequest(HttpMethod.Get, jsonUrl);
            var jsonResp = await SendAsync(jsonReq);
            if (!jsonResp.IsSuccess)
                throw new e621ClientException(ClientErrorType.Network, "Failed to fetch posts JSON") { Content = jsonResp.ContentText };

            if (string.IsNullOrWhiteSpace(jsonResp.ContentText))
                throw new e621ClientException(ClientErrorType.Network, "Empty response content");

            ePosts? posts;
            try
            {
                posts = JsonSerializer.Deserialize<ePosts>(jsonResp.ContentText);
            }
            catch (Exception ex)
            {
                throw new e621ClientException(ClientErrorType.Deserialization, $"JSON deserialization failed: {ex.Message}") { Content = jsonResp.ContentText };
            }

            if (posts == null)
                throw new e621ClientException(ClientErrorType.Deserialization, "Server returned an unexpected JSON payload") { Content = jsonResp.ContentText };

            posts.Query = tags ?? string.Empty;
            posts.Page = page;
            posts.Limit = limit;

            // Optionally attempt to fetch MaxPage by scraping the HTML (fragile)
            if (fetchMaxPage)
            {
                try
                {
                    var htmlReq = BuildRequest(HttpMethod.Get, htmlUrl);
                    var htmlResp = await SendAsync(htmlReq);
                    if (htmlResp.IsSuccess && !string.IsNullOrWhiteSpace(htmlResp.ContentText))
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(htmlResp.ContentText);

                        // This XPath is taken from your original code; it may break if site changes.
                        var paginator = doc.DocumentNode.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[3]/div[4]/nav");
                        if (paginator != null)
                        {
                            var nodes = paginator.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToList();
                            var lastNode = nodes.FindLast(n =>
                                n.GetAttributeValue("class", "").Contains("page last") ||
                                n.GetAttributeValue("class", "").Contains("page current")
                            );

                            if (lastNode != null && lastNode.ChildNodes.Count > 0)
                            {
                                if (int.TryParse(lastNode.ChildNodes[0].InnerText.Trim(), out int maxPage))
                                    posts.MaxPage = maxPage;
                            }
                        }
                    }
                }
                catch
                {
                    // Silently ignore HTML scraping failures; it's optional.
                }
            }

            return posts;
        }

        /// <summary>
        /// Scrapes a pool page (HTML) and returns partial posts for that pool.
        /// </summary>
        public async Task<ePosts> GetPoolPostsAsync(int poolId, int page = 1)
        {
            var url = GetUri(Host, $"pools/{poolId}");
            if (page > 1) url += $"?page={page}";

            var req = BuildRequest(HttpMethod.Get, url);
            var resp = await SendAsync(req);
            if (!resp.IsSuccess || string.IsNullOrWhiteSpace(resp.ContentText))
                throw new e621ClientException(ClientErrorType.Network, "Failed to fetch pool HTML") { Content = resp.ContentText };

            var doc = new HtmlDocument();
            doc.LoadHtml(resp.ContentText);

            var posts = new ePosts
            {
                Mode = ListMode.Pools,
                PoolId = poolId,
                Page = page,
                MaxPage = 750 // fallback default if parsing fails
            };

            var listNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"posts\"]/section");
            if (listNode == null)
                throw new e621ClientException(ClientErrorType.Deserialization, "Unable to find posts list in pool HTML") { Content = resp.ContentText };

            var paginator = doc.DocumentNode.SelectSingleNode("//*[@id=\"c-pools\"]/div[2]/menu");
            if (paginator != null)
            {
                var nodes = paginator.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToList();
                var lastNode = nodes.FindLast(n =>
                    n.GetAttributeValue("class", "").Contains("numbered-page") ||
                    n.GetAttributeValue("class", "").Contains("current-page")
                );

                if (lastNode != null && lastNode.ChildNodes.Count > 0)
                {
                    if (int.TryParse(lastNode.ChildNodes[0].InnerText.Trim(), out int maxPage))
                        posts.MaxPage = maxPage;
                }
            }

            foreach (var node in listNode.ChildNodes)
            {
                if (node.Name != "article") continue;

                try
                {
                    var post = new ePost
                    {
                        IsPartial = true,
                        Id = node.GetAttributeValue("data-id", 0),
                        Tags = new eTags()
                    };

                    post.Rating = node.GetAttributeValue("data-rating", "s");
                    post.FavCount = node.GetAttributeValue("data-fav-count", 0);
                    post.Preview.Url = node.GetAttributeValue("data-preview-url", "");
                    post.Preview.Width = node.GetAttributeValue("data-preview-width", 0);
                    post.Preview.Height = node.GetAttributeValue("data-preview-height", 0);
                    post.Sample.Url = node.GetAttributeValue("data-large-url", "");
                    post.File.Url = node.GetAttributeValue("data-file-url", "");
                    post.Score = new eScore { Total = node.GetAttributeValue("data-score", 0) };

                    var tagText = node.GetAttributeValue("data-tags", "");
                    if (!string.IsNullOrWhiteSpace(tagText))
                        post.Tags.General.AddRange(tagText.Split(' ', StringSplitOptions.RemoveEmptyEntries));

                    posts.Posts.Add(post);
                }
                catch (Exception ex)
                {
                    // Best-effort: log and continue
                    Console.WriteLine($"Unable to parse partial post: {ex.Message}");
                }
            }

            return posts;
        }

        #endregion

        #region Request plumbing

        private HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            // Always set user-agent from options (per-request)
            if (!string.IsNullOrWhiteSpace(Options.UserAgent))
            {
                // Try set normally, fallback to TryAddWithoutValidation
                try
                {
                    request.Headers.UserAgent.ParseAdd(Options.UserAgent);
                }
                catch
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", Options.UserAgent);
                }
            }

            // Credentials (per-request)
            if (Options.Credentials != null)
            {
                var token = ToBase64($"{Options.Credentials.Username}:{Options.Credentials.APIKey}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }

            return request;
        }

        private async Task<_InternalResponse> SendAsync(HttpRequestMessage request)
        {
            if (DebugMode)
                Console.WriteLine($"[{DateTime.UtcNow:O}] {request.Method} {request.RequestUri}");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);

            var headers = response.Headers.ToDictionary(k => k.Key, v => v.Value);
            string? content = null;

            // Read content as string if available
            if (response.Content != null)
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var success = response.IsSuccessStatusCode;

            if (DebugMode)
            {
                Console.WriteLine($"Response: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(content))
                    Console.WriteLine(content);
            }

            return new _InternalResponse(success, content, headers, response);
        }

        #endregion

        #region Utilities

        public static string GetUri(string host, string relative)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrWhiteSpace(relative)) return new Uri(host).AbsoluteUri;

            var baseUri = new Uri(host.EndsWith("/") ? host : host + "/");
            var result = new Uri(baseUri, relative);
            return result.AbsoluteUri;
        }

        /// <summary>
        /// Encode text to Base64 using Latin1 (ISO-8859-1) encoding per RFC7617.
        /// Accepts "username:password" formatted string.
        /// </summary>
        private static string ToBase64(string toEncode)
        {
            if (toEncode == null) throw new ArgumentNullException(nameof(toEncode));
            return Convert.ToBase64String(Encoding.GetEncoding("iso-8859-1").GetBytes(toEncode));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _http?.Dispose();
            _disposed = true;
        }

        #endregion

        #region Internal helpers & response types
        /// <summary>
        /// Internal simple response wrapper for plumbing.
        /// </summary>
        private readonly struct _InternalResponse
        {
            public bool IsSuccess { get; }
            public string? ContentText { get; }
            public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }
            public HttpResponseMessage Message { get; }

            public _InternalResponse(bool success, string? content, IDictionary<string, IEnumerable<string>> headers, HttpResponseMessage message)
            {
                IsSuccess = success;
                ContentText = content;
                Headers = new Dictionary<string, IEnumerable<string>>(headers ?? new Dictionary<string, IEnumerable<string>>());
                Message = message;
            }
        }

        #endregion
    }

    #region Client Classes
    [Serializable]
    internal class e621ClientHeader
    {
        public string Name { get; set; }
        public bool IsSingle { get; private set; }
        public string? SingleValue { get; set; }
        public List<string> Values { get; set; } = new List<string>();

        public e621ClientHeader(string name, string value)
        {
            Name = name;
            IsSingle = true;
            SingleValue = value;
            Values.Add(value);
        }
        public e621ClientHeader(string name, List<string> values)
        {
            IsSingle = false;
            Name = name;
            Values = values;
        }
        public e621ClientHeader(KeyValuePair<string, IEnumerable<string>> valuePair)
        {
            Name = valuePair.Key;
            foreach (var value in valuePair.Value)
                Values.Add(value);
        }
    }

    [Serializable]
    internal class e621ClientRequest
    {
        public string RelativeUrl { get; set; }
        internal List<e621ClientHeader> Headers { get; set; } = new List<e621ClientHeader>();

        public e621ClientRequest(string url, List<e621ClientHeader> headers)
        {
            RelativeUrl = url;
            Headers = headers;
        }
    }

    [Serializable]
    internal class e621ClientResponse
    {
        public RipperResponseType ResponseType { get; private set; }
        public bool IsContentFetched { get; private set; }
        public string? Content { get; private set; }
        public byte[]? Bytes { get; private set; }
        internal List<e621ClientHeader> Headers { get; private set; } = new List<e621ClientHeader>();
        public HttpResponseMessage? Message { get; private set; }

        public e621ClientResponse() { }
        public e621ClientResponse(HttpResponseMessage msg)
        {
            ResponseType = RipperResponseType.Error;
            Message = msg;
        }

        internal e621ClientResponse(bool isFetched, string text, List<e621ClientHeader> headers, HttpResponseMessage msg)
        {
            ResponseType = RipperResponseType.String;
            IsContentFetched = isFetched;
            Headers = headers;
            Content = text;
            Message = msg;
        }

        internal e621ClientResponse(bool isFetched, byte[] bytes, List<e621ClientHeader> headers, HttpResponseMessage msg)
        {
            ResponseType = RipperResponseType.Byte;
            IsContentFetched = isFetched;
            Headers = headers;
            Bytes = bytes;
            Message = msg;
        }
    }

    [Serializable]
    public class e621ClientException : Exception
    {
        public ClientErrorType ErrorType = ClientErrorType.Internal;
        public string? Content;

        public e621ClientException(string message) : base(message) { }
        public e621ClientException(ClientErrorType type, string message) : base(message) 
        { 
            ErrorType = type;
        }
    }
    #endregion

    #region Ripper Enums
    public enum ClientErrorType
    {
        Network,
        Internal,
        Deserialization
    }

    internal enum ClientRequestType
    {
        GET,
        POST
    }

    internal enum RipperResponseType
    {
        Error,
        String,
        Byte
    }
    #endregion
}
