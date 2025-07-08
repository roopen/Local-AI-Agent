IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.LocalAIAgent_API>("AINewsCurationAPI");

builder.AddNpmApp("AINewsCurationUI", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithEnvironment("PORT", "53146")
    .WithHttpsEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
