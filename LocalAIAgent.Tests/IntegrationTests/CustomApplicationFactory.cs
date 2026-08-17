using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });

                // 4) Build a temporary provider to initialize schema
                using ServiceProvider sp = services.BuildServiceProvider();
                using IServiceScope scope = sp.CreateScope();
                UserContext ctx = scope.ServiceProvider.GetRequiredService<UserContext>();

                ctx.Database.EnsureCreated();
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

    internal sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "IntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "integration-owner"),
                    new Claim(ClaimTypes.Role, AuthRoles.Owner),
                ],
                AuthenticationScheme);
            AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
