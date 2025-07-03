using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalAIAgent.Tests.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<API.Program>, IDisposable
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTests");

            builder.ConfigureServices(services =>
            {
                // 1) Remove the real DbContext
                ServiceDescriptor? descriptor = services.SingleOrDefault(
                  d => d.ServiceType == typeof(DbContextOptions<UserContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // 2) Create & open a shared in‐memory SQLite connection
                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                // 3) Register DbContext using that connection
                services.AddDbContext<UserContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                services.ConfigureHttpClientDefaults(http =>
                {
                    http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        CookieContainer = new System.Net.CookieContainer(),
                        UseCookies = true
                    });
                });

                // 4) Build a temporary provider to initialize schema
                using ServiceProvider sp = services.BuildServiceProvider();
                using IServiceScope scope = sp.CreateScope();
                UserContext ctx = scope.ServiceProvider.GetRequiredService<UserContext>();

                // OPTION A: Skip migrations, just create schema
                // ctx.Database.EnsureCreated();

                // OPTION B: If you want to actually test migrations, use:
                ctx.Database.EnsureDeleted();
                ctx.Database.Migrate();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }
    }
}
