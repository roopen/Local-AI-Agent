using LocalAIAgent.API.Api.Hubs;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.AddServiceDefaults();

            string sqldatasource = "DataSource=" + builder.Configuration.GetValue<string>("SQLITE_DATASOURCE");
            builder.Services.AddDbContext<UserContext>(options =>
                options.UseSqlite(sqldatasource));

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();
            builder.Services.AddSemanticKernel();
            builder.Services.AddScoped<SemanticKernel.Chat.IChatService, SemanticKernel.Chat.ChatService>();
            builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
            builder.Services.AddScoped<IGetUserUseCase, GetUserUseCase>();
            builder.Services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
            builder.Services.AddScoped<NewsMetrics>();
            builder.Services.AddMemoryCache();

            builder.Services.AddFido2(options =>
            {
                options.ServerDomain = "ainews.dev.localhost";
                options.ServerName = "AI News";
                options.Origins = new HashSet<string> { "https://ainews.dev.localhost:8888" };
            });

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
                dbContext.Database.Migrate();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();

                app.MapGet("/", context =>
                {
                    context.Response.Redirect("/swagger");
                    return Task.CompletedTask;
                });
            }

            app.UseHttpsRedirection();

            app.UseCors("AllowWebUI");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapDefaultEndpoints();
            app.MapControllers();
            app.MapHub<ChatHub>("/chatHub");
            app.MapHub<NewsHub>("/newsHub");

            // Run initial news fetch on startup
            InitializeNewsCache(app);
            LoadLLMOnStartup(app);

            app.Run();
        }

        private static void InitializeNewsCache(WebApplication app)
        {
            IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
            Task.Run(async () =>
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                INewsService newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
                await newsService.GetNewsAsync();
            });
        }

        private static void LoadLLMOnStartup(WebApplication app)
        {
            IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
            Task.Run(async () =>
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                ILoadLLMUseCase loadLLMUseCase = scope.ServiceProvider.GetRequiredService<ILoadLLMUseCase>();
                await loadLLMUseCase.LoadLLMUseCaseAsync();
            });
        }
    }
}
