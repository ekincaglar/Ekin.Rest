using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Ekin.Rest
{
    public class Client
    {
        #region Local Parameters
        
        private string _url { get; set; }
        private ICredentials _credentials { get; set; }
        private WebHeaderCollection _headers { get; set; }
        private int _timeout { get; set; }
        private string _values { get; set; }
        private int _retryCount = 1;
        private int _sleepBetweenRetries = 0;

        #endregion

        #region Public Parameters

        public string ContentType = "application/json; charset=utf-8";
        public string Accept = "application/json";
        public Type ErrorType = null;
        public bool EnableGzipEncoding = true;

        #endregion

        #region Constructors

        public Client(string url, ICredentials credentials, int timeout, int retryCount, int sleepBetweenRetries)
        {
            _url = url;
            _credentials = credentials;
            _timeout = timeout;
            _retryCount = retryCount;
            _sleepBetweenRetries = sleepBetweenRetries;
        }

        public Client(string url, ICredentials credentials, int timeout)
        {
            _url = url;
            _credentials = credentials;
            _timeout = timeout;
        }

        public Client(string url, ICredentials credentials)
        {
            _url = url;
            _credentials = credentials;
        }

        public Client(string url, int timeout)
        {
            _url = url;
            _credentials = CredentialCache.DefaultCredentials;
            _timeout = timeout;
        }

        public Client(string url)
        {
            _url = url;
            _credentials = CredentialCache.DefaultCredentials;
        }

        public Client(string url, WebHeaderCollection headers, int timeout, int retryCount, int sleepBetweenRetries)
        {
            _url = url;
            _headers = headers;
            _timeout = timeout;
            _retryCount = retryCount;
            _sleepBetweenRetries = sleepBetweenRetries;
        }

        public Client(string url, WebHeaderCollection headers, int timeout)
        {
            _url = url;
            _headers = headers;
            _timeout = timeout;
        }

        public Client(string url, WebHeaderCollection headers)
        {
            _url = url;
            _headers = headers;
        }

        #endregion

        #region Web requests are handled here

        private Response Execute(WebRequestMethod method)
        {
            Response response = new Response();

            if (String.IsNullOrWhiteSpace(_url))
            {
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException("URL empty");
                return response;
            }

            // Url should be truncated to 2000 characters or less
            // TODO: We should notify the caller that we're changing their url
            if (_url.Length > 2000)
            {
                _url = _url.Substring(0, 2000);
                if (_url.Contains("%2C")) _url = _url.Substring(0, _url.LastIndexOf("%2C"));
            }

            // Prepare the http request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);
            if (_timeout > 0)
                request.Timeout = _timeout;
            if (_credentials != null)
                request.Credentials = _credentials;
            if (_headers != null)
                request.Headers = _headers;
            else
                request.Headers = new System.Net.WebHeaderCollection();
            if (EnableGzipEncoding)
            {
                request.Headers.Add(System.Net.HttpRequestHeader.AcceptEncoding, "gzip");
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            }
            request.ContentType = this.ContentType;
            request.Accept = this.Accept;

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
                    break;

                case WebRequestMethod.Delete:
                    request.Method = method.ToEnumString();
                    break;

                default:
                    break;
            }

            // Get the http response
            HttpWebResponse webResponse = null;
            int CurrentRetryCount = _retryCount > 1 ? _retryCount : 1;
            while (CurrentRetryCount > 0)
            {
                try
                {
                    webResponse = (HttpWebResponse)request.GetResponse();
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

                    if (_sleepBetweenRetries > 0)
                    {
                        System.Threading.Thread.Sleep(_sleepBetweenRetries);
                    }
                }
            }
            if (webResponse != null)
            {
                response.Status = webResponse.StatusCode;
                response.StatusDescription = webResponse.StatusDescription;
                response.Content = GetContent(webResponse);

                // Close the HttpWebResponse
                webResponse.Close();
            }
            else
            {
                response.Status = HttpStatusCode.RequestTimeout;
                response.StatusDescription = $"No response could be retrieved from destination after {_retryCount} retries";
                response.Content = string.Empty;
            }

            return response;
        }

        internal string GetContent(WebResponse webResponse)
        {
            string response = "";
            try
            {
                // Convert the response to plain text
                Stream dataStream = webResponse.GetResponseStream();
                using (StreamReader reader = new StreamReader(dataStream))
                {
                    response = reader.ReadToEnd();
                    reader.Close();
                }
            }
            catch (Exception ex)
            {

            };
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

        public Response Get()
        {
            return Execute(WebRequestMethod.Get);
        }

        public Response Post(string values)
        {
            _values = values;
            return Execute(WebRequestMethod.Post);
        }

        /// <summary>
        /// Post method
        /// </summary>
        /// <param name="obj">Object to post to the API</param>
        /// <param name="serializeNullValues">Should the NULL values in the object be serialised when sent to the server as JSON</param>
        /// <returns></returns>
        public Response Post(object obj, bool serializeNullValues = false, bool AllowReferenceLoops = true)
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

                return Execute(WebRequestMethod.Post);
            }
            catch (Exception ex)
            {
                Response response = new Response();
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException(String.Format("JSON Serialization Error: {0}", ex.Message));
                return response;
            }
        }

        public Response Put(string values)
        {
            _values = values;
            return Execute(WebRequestMethod.Put);
        }

        public Response Put(object obj)
        {
            try
            {
                // TODO: Check if JsonConvert.SerializeObject does the equivalent of Uri.EscapeDataString("escape me")
                _values = JsonConvert.SerializeObject(obj);
                return Execute(WebRequestMethod.Put);
            }
            catch (Exception ex)
            {
                Response response = new Response();
                response.Status = HttpStatusCode.Unused;
                response.InternalError = new WebException(String.Format("JSON Serialization Error: {0}", ex.Message));
                return response;
            }
        }

        public Response Delete()
        {
            return Execute(WebRequestMethod.Delete);
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