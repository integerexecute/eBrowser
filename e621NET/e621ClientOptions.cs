namespace e621NET
{
    public class e621ClientOptions
    {
        public string UserAgent { get; set; } = "e621NET/0.1 (+https://disotakyu.vercel.app/e621)";
        public string HostUri { get; set; } = "https://e621.net/";
        public e621APICredentials? Credentials { get; set; }
        public int TimeoutSeconds { get; set; } = 100;
    }

    public class e621APICredentials
    { 
        public string Username { get; set; }
        public string APIKey { get; set; }

        public e621APICredentials(string username, string apiKey)
        {
            Username = username;
            APIKey = apiKey;
        }
    }
}
