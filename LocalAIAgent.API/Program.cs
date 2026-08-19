using LocalAIAgent.API.Api.Hubs;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Application;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using Microsoft.AspNetCore.DataProtection;
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

                Uri publicOrigin = builder.ConfigurePublicOriginAndProxy();

                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Configure(builder.Configuration.GetSection("Kestrel"));
                });

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localAppFolder = Path.Combine(localAppData, "LocalAIAgent");
                string? configuredKeysPath = builder.Configuration["DATA_PROTECTION_KEYS_PATH"];
                if (string.IsNullOrWhiteSpace(configuredKeysPath))
                    Directory.CreateDirectory(localAppFolder);
                string dataProtectionKeysPath = configuredKeysPath
                    ?? Path.Combine(localAppFolder, "DataProtectionKeys");
                builder.Services.AddDataProtection()
                    .SetApplicationName("LocalAIAgent")
                    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
                builder.Services.AddSingleton<IAiSettingsSecretProtector, AiSettingsSecretProtector>();
                builder.Services.AddScoped<AiSettingsStartupService>();

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
                        Directory.CreateDirectory(localAppFolder);
                        connectionString = Path.Combine(localAppFolder, "ainews.db");
                    }
                }

                string sqldatasource = "DataSource=" + connectionString;
                builder.Services.AddDbContext<UserContext>(options =>
                    options.UseSqlite(sqldatasource));

                builder.Services.AddControllers();
                builder.AddRequestSecurity();
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
                builder.Services.AddSignalR();
                builder.Services.AddApplicationServices(builder.Configuration);
                builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
                builder.Services.AddScoped<IGetUserUseCase, GetUserUseCase>();
                builder.Services.AddScoped<IGetDatasetUseCase, GetDatasetUseCase>();
                builder.Services.AddScoped<INewsDatasetRepository, NewsDatasetRepository>();
                builder.Services.AddScoped<IArticleTranslationRepository, ArticleTranslationRepository>();
                builder.Services.AddScoped<ICustomFeedRepository, CustomFeedRepository>();
                builder.Services.AddScoped<NewsMetrics>();
                builder.Services.AddMemoryCache();
                builder.Services.AddDistributedMemoryCache();

                builder.AddPasskeys(publicOrigin);

                if (builder.Environment.IsDevelopment())
                {
                    builder.Services.AddCors(options =>
                    {
                        options.AddPolicy("AllowWebUI", policy =>
                        {
                            policy.WithOrigins("https://ainews.dev.localhost:8888")
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                        });
                    });
                }

                WebApplication app = builder.Build();

                bool isIntegrationTests = app.Environment.IsEnvironment("IntegrationTests");
                bool isSwaggerGen = app.Environment.IsEnvironment("SwaggerGeneration");

                // Ensure database is created and migrations are applied
                using (IServiceScope scope = app.Services.CreateScope())
                {
                    UserContext dbContext = scope.ServiceProvider.GetRequiredService<UserContext>();
                    if (!isIntegrationTests && !isSwaggerGen)
                    {
                        dbContext.Database.Migrate();
                        scope.ServiceProvider.GetRequiredService<AiSettingsStartupService>()
                            .UpgradePlaintextTokensAsync().GetAwaiter().GetResult();
                    }
                }

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                    app.UseCors("AllowWebUI");

                app.UseRequestSecurity();

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

                app.UseDefaultFiles();
                app.UseStaticFiles();

                app.MapDefaultEndpoints();
                app.MapControllers();
                app.MapHub<NewsHub>("/newsHub");
                app.MapFallbackToFile("index.html");

                if (!isIntegrationTests && !isSwaggerGen)
                {
                    using IServiceScope startupScope = app.Services.CreateScope();
                    startupScope.ServiceProvider.GetRequiredService<AiSettingsStartupService>()
                        .ActivateFirstAndWarmUpAsync().GetAwaiter().GetResult();

                    if (app.Environment.IsDevelopment())
                        app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(app));
                }

                app.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void OpenBrowser(WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
                return;

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

    }
}
