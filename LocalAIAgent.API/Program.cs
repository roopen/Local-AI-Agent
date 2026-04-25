using LocalAIAgent.API.Api.Hubs;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;
using System.Globalization;

namespace LocalAIAgent.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();

            try
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

                builder.Host.UseSerilog();

                builder.AddServiceDefaults();

                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Configure(builder.Configuration.GetSection("Kestrel"));
                });

                string? httpsUrl = builder.Configuration.GetValue<string>("Kestrel:Endpoints:Https:Url");
                if (!string.IsNullOrEmpty(httpsUrl))
                {
                    builder.WebHost.UseUrls(httpsUrl);
                }

                string? connectionString = builder.Configuration.GetValue<string>("SQLITE_DATASOURCE");
                if (string.IsNullOrEmpty(connectionString))
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        connectionString = "ainews.db";
                    }
                    else
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string appFolder = Path.Combine(appData, "LocalAIAgent");
                        Directory.CreateDirectory(appFolder);
                        connectionString = Path.Combine(appFolder, "ainews.db");
                    }
                }

                string sqldatasource = "DataSource=" + connectionString;
                builder.Services.AddDbContext<UserContext>(options =>
                    options.UseSqlite(sqldatasource));

                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
                builder.Services.AddSignalR();
                builder.Services.AddSemanticKernel(builder.Configuration);
                builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
                builder.Services.AddScoped<IGetUserUseCase, GetUserUseCase>();
                builder.Services.AddScoped<IGetDatasetUseCase, GetDatasetUseCase>();
                builder.Services.AddScoped<INewsDatasetRepository, NewsDatasetRepository>();
                builder.Services.AddScoped<IArticleTranslationRepository, ArticleTranslationRepository>();
                builder.Services.AddScoped<NewsMetrics>();
                builder.Services.AddMemoryCache();
                builder.Services.AddDistributedMemoryCache();

                builder.Services.AddFido2(options =>
                {
                    options.ServerDomain = "ainews.dev.localhost";
                    options.ServerName = "AI News";
                    options.Origins = new HashSet<string>
                    {
                    "https://ainews.dev.localhost:8888",
                    "https://ainews.dev.localhost:7276",
                    "https://apiainews.dev.localhost:7276",
                    "https://localhost:7276"
                    };
                })
                    .AddCachedMetadataService(config =>
                    {
                        config.AddFidoMetadataRepository();
                    });

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowWebUI", policy =>
                    {
                        policy.WithOrigins(
                            "https://ainews.dev.localhost:8888",
                            "https://apiainews.dev.localhost:7276",
                            "https://localhost",
                            "https://ainews.dev.localhost:7276",
                            "https://localhost:7276")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
                });

                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                        options.ExpireTimeSpan = TimeSpan.FromMinutes(3600);
                        options.LoginPath = "/api/Login/login";
                        options.AccessDeniedPath = "/";
                    });


                WebApplication app = builder.Build();

                // Ensure database is created and migrations are applied
                using (IServiceScope scope = app.Services.CreateScope())
                {
                    UserContext dbContext = scope.ServiceProvider.GetRequiredService<UserContext>();
                    if (!app.Environment.IsEnvironment("IntegrationTests"))
                        dbContext.Database.Migrate();
                }

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();

                    app.MapGet("/api", context =>
                    {
                        context.Response.Redirect("/swagger");
                        return Task.CompletedTask;
                    });
                }

                app.UseHttpsRedirection();

                app.UseDefaultFiles();
                app.UseStaticFiles();

                app.UseCors("AllowWebUI");

                app.UseAuthentication();
                app.UseAuthorization();

                app.MapDefaultEndpoints();
                app.MapControllers();
                app.MapHub<ChatHub>("/chatHub");
                app.MapHub<NewsHub>("/newsHub");

                // Run initial news fetch on startup
                InitializeNewsCache(app);
                LoadLLMOnStartup(app).Wait();

                app.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void InitializeNewsCache(WebApplication app)
        {
            IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
            Task.Run(async () =>
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                INewsService newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
                await newsService.GetNewsAsync();

                if (!app.Lifetime.ApplicationStarted.IsCancellationRequested)
                {
                    TaskCompletionSource<object> tcs = new();
                    using CancellationTokenRegistration reg = app.Lifetime.ApplicationStarted.Register(() => tcs.SetResult(new object()));
                    await tcs.Task;
                }

                OpenBrowser(app);
            });
        }

        private static void OpenBrowser(WebApplication app)
        {
            try
            {
                string? url = app.Urls.FirstOrDefault(static u => u.StartsWith("https", StringComparison.InvariantCultureIgnoreCase)) ?? app.Urls.FirstOrDefault();
                if (!string.IsNullOrEmpty(url))
                {
                    url = url.Replace("0.0.0.0", "localhost")
                             .Replace("[::]", "localhost")
                             .Replace("+", "localhost")
                             .Replace("*", "localhost");

                    if (app.Environment.IsDevelopment())
                        url = "https://ainews.dev.localhost:7276/";

                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch browser: {ex.Message}");
            }
        }

        private static async Task LoadLLMOnStartup(WebApplication app)
        {
            IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
            await Task.Run(async () =>
            {
                using IServiceScope scope = scopeFactory.CreateScope();

                UserContext userContext = scope.ServiceProvider.GetRequiredService<UserContext>();
                AiSettings? settings = await userContext.AiSettings.FirstOrDefaultAsync();

                IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                if (settings is not null)
                {
                    configuration["AIOptions:ModelId"] = settings.ModelId;
                    configuration["AIOptions:ApiKey"] = settings.ApiKey;
                    configuration["AIOptions:EndpointUrl"] = settings.EndpointUrl;
                    configuration["AIOptions:Temperature"] = settings.Temperature.ToString(CultureInfo.InvariantCulture);
                    configuration["AIOptions:TopP"] = settings.TopP.ToString(CultureInfo.InvariantCulture);
                    configuration["AIOptions:FrequencyPenalty"] = settings.FrequencyPenalty.ToString(CultureInfo.InvariantCulture);
                    configuration["AIOptions:PresencePenalty"] = settings.PresencePenalty.ToString(CultureInfo.InvariantCulture);
                }

                ILoadLLMUseCase loadLLMUseCase = scope.ServiceProvider.GetRequiredService<ILoadLLMUseCase>();
                await loadLLMUseCase.LoadLLMUseCaseAsync();
            });
        }
    }
}
