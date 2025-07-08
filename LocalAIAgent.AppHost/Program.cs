IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.LocalAIAgent_API>("ainewscurationapi");

#if DEBUG
builder.AddNpmApp("ainewscurationui", "../LocalAIAgent.WebUI", "dev")
    .WithReference(apiService)
    .WithEnvironment("PORT", "53146")
    .WithHttpsEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();
#endif

builder.Build().Run();
