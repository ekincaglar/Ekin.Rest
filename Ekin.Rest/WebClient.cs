using System;
using System.Net;

namespace Ekin.Rest.CookieEnabled
{
    public class WebClient : System.Net.WebClient
    {
        public CookieContainer CookieContainer { get; private set; }

        public WebClient()
        {
            CookieContainer = new CookieContainer();
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            request.CookieContainer = CookieContainer;
            return request;
        }
    }
}
