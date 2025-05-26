using LocalAIAgent.API.Hubs;
using LocalAIAgent.SemanticKernel;

namespace LocalAIAgent.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();
            builder.Services.AddSemanticKernel();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    policy => policy.WithOrigins("http://localhost:53146") // Set the allowed frontend URL
                                    .AllowAnyMethod()
                                    .AllowAnyHeader()
                                    .AllowCredentials()); // This enables cookies/auth tokens
            });


            WebApplication app = builder.Build();

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

                app.UseCors("AllowSpecificOrigin");
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<ChatHub>("/chatHub").RequireCors("AllowSpecificOrigin");

            app.Run();
        }
    }
}
