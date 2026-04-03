using Aspire.Hosting.JavaScript;
using Serilog;
using System.Globalization;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<SqliteResource> sqlite = builder.AddSqlite("sqlite", "../LocalAIAgent.API/", "AINews.db");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.LocalAIAgent_API>("AINewsCurationAPI").WithReference(sqlite);

IResourceBuilder<JavaScriptAppResource> frontend = builder.AddJavaScriptApp("ainews", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithHttpsEndpoint(port: 8888, env: "PORT", name: "https")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.IsProxied = false;
        endpoint.TargetHost = "ainews.dev.localhost";
    });

Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();

builder.Build().Run();
