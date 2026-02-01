IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite", "../LocalAIAgent.API/", "AINews.db");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.LocalAIAgent_API>("AINewsCurationAPI").WithReference(sqlite);

var frontend = builder.AddNpmApp("ainews", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithHttpsEndpoint(port: 8888, env: "PORT", name: "https")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.IsProxied = false;
        endpoint.TargetHost = "ainews.dev.localhost";
    });

builder.Build().Run();
