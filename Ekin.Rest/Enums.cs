namespace Ekin.Rest
{
    public enum WebRequestMethod { Get, Post, Put, Delete }

    public static class WebRequestMethodExtensions
    {
        public static string ToEnumString(this WebRequestMethod me)
        {
            switch (me)
            {
                case WebRequestMethod.Get: return "GET";
                case WebRequestMethod.Post: return "POST";
                case WebRequestMethod.Put: return "PUT";
                case WebRequestMethod.Delete: return "DELETE";
                default: return "ERROR";
            }
        }
    }
}
