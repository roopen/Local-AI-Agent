﻿using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class FoxNewsSettings : INewsClientSettings
    {
        public string ClientName => "FoxNewsClient";
        public string BaseUrl => "https://moxie.foxnews.com/google-publisher/";
        public string UserAgent = "Mozilla/5.0";
        public string Host = "moxie.foxnews.com";

        public string UsaNewsUrl = "us.xml";
        public string WorldNewsUrl = "world.xml";

        public List<string> GetNewsUrls()
        {
            return
            [
                UsaNewsUrl,
                WorldNewsUrl
            ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Host = Host;
            });
        }
    }
}
