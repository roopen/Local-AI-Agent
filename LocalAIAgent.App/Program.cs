using LocalAIAgent.App.Chat;
using LocalAIAgent.App.Extensions;
using LocalAIAgent.App.News;
using LocalAIAgent.App.RAG;
using LocalAIAgent.App.RAG.Embedding;
using LocalAIAgent.App.Time;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using NodaTime;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.Services.AddNewsClients();
kernelBuilder.Services.AddSingleton<ChatService>();
kernelBuilder.Services.AddSingleton<ChatContext>();
kernelBuilder.Services.AddSingleton<RAGService>();
kernelBuilder.Services.AddSingleton<NewsService>();

kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();
kernelBuilder.Services.AddSingleton<IClock>(SystemClock.Instance);
IConfiguration configuration = kernelBuilder.Services.AddConfigurations();
AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>() ?? throw new Exception("AIOptions not found in configuration.");

kernelBuilder.Plugins.AddFromType<TimeService>();
kernelBuilder.Plugins.AddFromType<NewsService>();

kernelBuilder.AddVectorStoreTextSearch<NewsItem>();
kernelBuilder.AddInMemoryVectorStore();
//kernelBuilder.Services.AddInMemoryVectorStoreRecordCollection<int, NewsItem>("news");

#pragma warning disable SKEXP0070 
// Experimental Google Gemini support
//kernelBuilder.AddGoogleAIGeminiChatCompletion(aiOptions.ModelId, aiOptions.ApiKey);
#pragma warning restore SKEXP0070 

kernelBuilder
    .AddOpenAIChatCompletion(
        modelId: aiOptions.ModelId,
        apiKey: aiOptions.ApiKey,
        endpoint: new Uri(aiOptions.EndpointUrl)
    );

Kernel kernel = kernelBuilder.Build();

await kernel.StartAIChat(aiOptions);