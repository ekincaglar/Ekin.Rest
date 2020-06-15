using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ekin.Rest
{
    public class Client
    {
        #region Local Parameters
        
        private string _values { get; set; }

        #endregion

        #region Public Parameters

        public string Url { get; set; }
        public ICredentials Credentials { get; set; }
        public WebHeaderCollection Headers { get; set; }
        public string ContentType { get; set; } = "application/json; charset=utf-8";
        public string Accept { get; set; } = "application/json";
        public Type ErrorType { get; set; } = null;
        public bool EnableGzipEncoding { get; set; } = true;
        public int Timeout { get; set; }
        public int RetryCount { get; set; } = 1;
        public int SleepBetweenRetries { get; set; } = 0;
        public string UserAgent { get; set; } = "EkinRest/2.2.0";
        public bool KeepAlive { get; set; } = true;
        public IWebProxy Proxy { get; set; } = null;
        public RequestCachePolicy CachePolicy { get; set; } = null; // new RequestCachePolicy(RequestCacheLevel.Default);

        #endregion

        #region Constructors

        public Client(string url, ICredentials credentials, int timeout, int retryCount, int sleepBetweenRetries)
        {
            Url = url;
            Credentials = credentials;
            Timeout = timeout;
            RetryCount = retryCount;
            SleepBetweenRetries = sleepBetweenRetries;
        }

        public Client(string url, ICredentials credentials, int timeout)
        {
            Url = url;
            Credentials = credentials;
            Timeout = timeout;
        }

        public Client(string url, ICredentials credentials)
        {
            Url = url;
            Credentials = credentials;
        }

        public Client(string url, int timeout)
        {
            Url = url;
            Credentials = CredentialCache.DefaultCredentials;
            Timeout = timeout;
        }

        public Client(string url)
        {
            Url = url;
            Credentials = CredentialCache.DefaultCredentials;
        }

        public Client(string url, WebHeaderCollection headers, int timeout, int retryCount, int sleepBetweenRetries)
        {
            Url = url;
            Headers = headers;
            Timeout = timeout;
            RetryCount = retryCount;
            SleepBetweenRetries = sleepBetweenRetries;
        }

        public Client(string url, WebHeaderCollection headers, int timeout)
        {
            Url = url;
            Headers = headers;
            Timeout = timeout;
        }

        public Client(string url, WebHeaderCollection headers)
        {
            Url = url;
            Headers = headers;
        }

        #endregion

        #region Web requests are handled here

        private async Task<Response> Execute(WebRequestMethod method)
        {
            Response response = new Response();

            if (String.IsNullOrWhiteSpace(Url))
            {
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException("URL empty");
                return response;
            }

            // Url should be truncated to 2000 characters or less
            // TODO: We should notify the caller that we're changing their url
            if (Url.Length > 2000)
            {
                Url = Url.Substring(0, 2000);
                if (Url.Contains("%2C")) Url = Url.Substring(0, Url.LastIndexOf("%2C"));
            }

            // Prepare the http request
            HttpWebRequest request = CreateHttpRequest(method);

            // Get the http response
            bool isResponseProcessed = false;
            int CurrentRetryCount = RetryCount > 1 ? RetryCount : 1;
            while (CurrentRetryCount > 0)
            {
                try
                {
                    //using (HttpWebResponse webResponse = (HttpWebResponse)await request.GetResponseAsync())
                    using (HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse())
                    {
                        response.Status = webResponse.StatusCode;
                        response.StatusDescription = webResponse.StatusDescription;
                        response.Content = GetContent(webResponse);
                        isResponseProcessed = true;
                    }
                    break;
                }
                catch (WebException ex)
                {
                    CurrentRetryCount--;

                    if (CurrentRetryCount == 0)
                    {
                        response.InternalError = GetError(ex);
                        if (ex.Status == WebExceptionStatus.ProtocolError)
                            response.Status = ((HttpWebResponse)ex.Response).StatusCode;
                        else
                            response.Status = HttpStatusCode.Unused;
                        return response;
                    }

                    if (SleepBetweenRetries > 0)
                    {
                        await Task.Delay(SleepBetweenRetries);
                    }
                }
            }

            if (!isResponseProcessed)
            {
                response.Status = HttpStatusCode.RequestTimeout;
                response.StatusDescription = $"No response could be retrieved from destination after {RetryCount} retries";
                response.Content = string.Empty;
            }

            return response;
        }

        private HttpWebRequest CreateHttpRequest(WebRequestMethod method)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Accept = Accept;
            request.ContentType = ContentType;
            request.UserAgent = UserAgent;
            request.KeepAlive = KeepAlive;
            request.Proxy = Proxy;
            request.CachePolicy = CachePolicy;

            if (Timeout > 0)
                request.Timeout = Timeout;

            if (Credentials != null)
                request.Credentials = Credentials;

            request.Headers = Headers == null ? new System.Net.WebHeaderCollection() : Headers;

            if (EnableGzipEncoding)
            {
                request.Headers.Add(System.Net.HttpRequestHeader.AcceptEncoding, "gzip");
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            }

            switch (method)
            {
                case WebRequestMethod.Get:
                    request.Method = method.ToEnumString();
                    break;

                case WebRequestMethod.Post:
                    request.Method = method.ToEnumString();
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
                        streamWriter.Write(_values);
                        streamWriter.Flush();
                    }
                    break;

                case WebRequestMethod.Put:
                    request.Method = method.ToEnumString();
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
                        streamWriter.Write(_values);
                        streamWriter.Flush();
                    }
                    //bodyStream.BaseStream.Seek(0, SeekOrigin.Begin);
                    break;

                case WebRequestMethod.Delete:
                    request.Method = method.ToEnumString();
                    break;

                default:
                    break;
            }

            return request;
        }

        internal string GetContent(WebResponse webResponse)
        {
            string response = string.Empty;
            if (webResponse != null)
            {
                try
                {
                    // Convert the response to plain text
                    using (Stream stream = webResponse.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            response = reader.ReadToEnd();
                        }
                    }
                }
                catch
                {

                };
            }
            return response;
        }

        private object GetError(WebException ex)
        {
            string response = GetContent(ex.Response);
            if (!String.IsNullOrWhiteSpace(response) && ErrorType != null)
            {
                try
                {
                    return JsonConvert.DeserializeObject(response, ErrorType);
                }
                catch (Exception exception)
                {

                }
            }
            return ex;
        }

        #endregion

        #region Get / Post / Put / Delete methods

        public async Task<Response> Get()
        {
            return await Execute(WebRequestMethod.Get);
        }

        public async Task<Response> Post(string values)
        {
            _values = values;
            return await Execute(WebRequestMethod.Post);
        }

        /// <summary>
        /// Post method
        /// </summary>
        /// <param name="obj">Object to post to the API</param>
        /// <param name="serializeNullValues">Should the NULL values in the object be serialised when sent to the server as JSON</param>
        /// <returns></returns>
        public async Task<Response> Post(object obj, bool serializeNullValues = false, bool AllowReferenceLoops = true)
        {
            try
            {
                // TODO: Check if JsonConvert.SerializeObject does the equivalent of Uri.EscapeDataString("escape me")
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = AllowReferenceLoops ? ReferenceLoopHandling.Serialize : ReferenceLoopHandling.Error,
                    NullValueHandling = serializeNullValues ? NullValueHandling.Include : NullValueHandling.Ignore
                };

                _values = JsonConvert.SerializeObject(obj, serializerSettings);

                return await Execute(WebRequestMethod.Post);
            }
            catch (Exception ex)
            {
                Response response = new Response();
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException(String.Format("JSON Serialization Error: {0}", ex.Message));
                return response;
            }
        }

        public async Task<Response> Put(string values)
        {
            _values = values;
            return await Execute(WebRequestMethod.Put);
        }

        public async Task<Response> Put(object obj, bool serializeNullValues = false, bool AllowReferenceLoops = true)
        {
            try
            {
                // TODO: Check if JsonConvert.SerializeObject does the equivalent of Uri.EscapeDataString("escape me")
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = AllowReferenceLoops ? ReferenceLoopHandling.Serialize : ReferenceLoopHandling.Error,
                    NullValueHandling = serializeNullValues ? NullValueHandling.Include : NullValueHandling.Ignore
                };

                _values = JsonConvert.SerializeObject(obj, serializerSettings);

                return await Execute(WebRequestMethod.Put);
            }
            catch (Exception ex)
            {
                Response response = new Response();
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException(String.Format("JSON Serialization Error: {0}", ex.Message));
                return response;
            }
        }

        public async Task<Response> Delete()
        {
            return await Execute(WebRequestMethod.Delete);
        }

        #endregion

        #region Url Redirection helper

        /// <summary>
        /// Recursively determines the final redirected url 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetRedirectedUrl(string url)
        {
            string redirectedUrl = String.Empty;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 10000;
            request.AllowAutoRedirect = false;
            try
            {
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.MovedPermanently ||
                        response.StatusCode == HttpStatusCode.Moved ||
                        response.StatusCode == HttpStatusCode.Found)
                    {
                        redirectedUrl = response.GetResponseHeader("Location");
                        if (redirectedUrl.StartsWith("/"))
                        {
                            redirectedUrl = new Uri(response.ResponseUri, redirectedUrl).ToString();
                        }
                    }
                    else
                    {
                        string responseText = "";
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream(), ASCIIEncoding.ASCII))
                        {
                            responseText = reader.ReadToEnd();
                        }

                        // Check if the page contains a Refresh metatag
                        Regex re = new Regex(@"http-equiv\W*refresh.+?url\W+?(.+)", RegexOptions.IgnoreCase);
                        Match match = re.Match(responseText);
                        if (match.Success)
                        {
                            int UrlStart = match.Value.IndexOf("url=", StringComparison.InvariantCultureIgnoreCase);
                            if (UrlStart > 0)
                            {
                                UrlStart += 4;
                                int UrlEnd = match.Value.IndexOf(">", UrlStart, StringComparison.InvariantCultureIgnoreCase);
                                if (UrlStart > 0 && UrlEnd > 0)
                                {
                                    redirectedUrl = match.Value.Substring(UrlStart, UrlEnd - UrlStart - 1);
                                }
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        return "ERROR: " + ex.Message;
                    }
                }
                else
                {
                    return "ERROR: Connection failure";
                }
            }
            if (String.IsNullOrWhiteSpace(redirectedUrl))
            {
                return url;     // There is no redirection
            }
            else if (redirectedUrl != url)
            {
                return GetRedirectedUrl(redirectedUrl);
            }
            else
            {
                return redirectedUrl;       // Does the code ever reach here?
            }
        }

        #endregion
    }

}