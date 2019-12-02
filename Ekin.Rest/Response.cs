using Newtonsoft.Json;
using System;
using System.Net;

namespace Ekin.Rest
{
    public class Response
    {
        public HttpStatusCode Status { get; set; }
        public string StatusDescription { get; set; }
        public string Content { get; set; }
        public object InternalError { get; set; }

        public Response() { }

        public dynamic GetObj(Type type)
        {
            dynamic ret = null;
            if (!String.IsNullOrWhiteSpace(Content))
            {
                try
                {
                    ret = JsonConvert.DeserializeObject(Content, type);
                }
                catch (Exception ex)
                {
                    // TODO: handle error
                }
            }
            return ret;
        }

    }
}