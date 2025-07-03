IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.LocalAIAgent_API>("apiservice");

builder.AddNpmApp("webfrontend", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithEnvironment("PORT", "53146")
    .WithHttpsEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
