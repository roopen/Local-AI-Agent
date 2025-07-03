using LocalAIAgent.API.Hubs;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.API.UseCases;
using LocalAIAgent.SemanticKernel;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.AddServiceDefaults();
            builder.Services.AddDbContext<UserContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();
            builder.Services.AddSemanticKernel();
            builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
            builder.Services.AddScoped<IGetUserUseCase, GetUserUseCase>();
            builder.Services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
            builder.Services.AddSingleton<NewsMetrics>();


            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowWebUI", policy =>
                {
                    policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
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

            app.UseAuthorization();

            app.MapDefaultEndpoints();
            app.MapControllers();
            app.MapHub<ChatHub>("/chatHub");

            app.Run();
        }
    }
}
