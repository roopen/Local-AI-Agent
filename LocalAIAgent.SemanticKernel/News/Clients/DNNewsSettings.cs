//using Microsoft.Extensions.DependencyInjection;

//namespace LocalAIAgent.SemanticKernel.News.Clients
//{
//    internal class DNNewsSettings : BaseNewsClientSettings
//    {
//        public override string ClientName => "DNNewsClient";
//        public override string BaseUrl => "https://www.dn.se";

//        public override List<string> GetNewsUrls()
//        {
//            return
//            [
//                $"{BaseUrl}/rss/",
//            ];
//        }

//        public override void AddHttpClient(IServiceCollection services)
//        {
//            services.AddHttpClient(ClientName, client =>
//            {
//                client.BaseAddress = new Uri(BaseUrl);
//                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
//                client.DefaultRequestHeaders.Host = Host;
//            })
//            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
//            {
//                AllowAutoRedirect = true
//            });
//        }
//    }
//}
