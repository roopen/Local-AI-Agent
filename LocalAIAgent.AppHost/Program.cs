IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite", "../LocalAIAgent.API/", "AINews.db");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.LocalAIAgent_API>("AINewsCurationAPI").WithReference(sqlite);

builder.AddNpmApp("ainews", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithEnvironment("PORT", "53146")
    .WithHttpsEndpoint(env: "PORT", name: "https")
    .PublishAsDockerFile();

builder.Build().Run();
