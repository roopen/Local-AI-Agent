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
        public string? Role { get; init; } = AuthRoles.Owner;
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
                }).AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    options => options.Role = Role);

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

    public sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string? Role { get; set; }
    }

    internal sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "IntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Options.Role is null)
                return Task.FromResult(AuthenticateResult.NoResult());
            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "integration-owner"),
                    new Claim(ClaimTypes.Role, Options.Role),
                ],
                AuthenticationScheme);
            AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
