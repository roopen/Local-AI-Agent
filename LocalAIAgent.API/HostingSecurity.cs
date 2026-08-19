using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API;

internal static class HostingSecurity
{
    public static Uri ConfigurePublicOriginAndProxy(this WebApplicationBuilder builder)
    {
        bool requiresConfiguredOrigin = !builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("IntegrationTests")
            && !builder.Environment.IsEnvironment("SwaggerGeneration");
        string? configuredOrigin = builder.Configuration["PUBLIC_ORIGIN"];
        if (requiresConfiguredOrigin && string.IsNullOrWhiteSpace(configuredOrigin))
            throw new InvalidOperationException("PUBLIC_ORIGIN is required outside development and test environments.");

        string value = configuredOrigin ?? "https://ainews.dev.localhost:7276";
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? origin)
            || origin.Scheme != Uri.UriSchemeHttps
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment)
            || !string.IsNullOrEmpty(origin.UserInfo))
        {
            throw new InvalidOperationException("PUBLIC_ORIGIN must be an HTTPS origin without a path.");
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options => ConfigureForwardedHeaders(
            options,
            builder.Configuration,
            origin,
            requiresConfiguredOrigin));
        builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
        {
            // Loopback hostnames keep local health checks functional; the host port is
            // bound only to 127.0.0.1 in the production Quadlet.
            options.AllowedHosts = [origin.Host, "localhost", "127.0.0.1", "[::1]"];
        });
        return origin;
    }

    public static void AddRequestSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "AINews.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(ConfigureCookie);
    }

    public static void AddPasskeys(this WebApplicationBuilder builder, Uri publicOrigin)
    {
        HashSet<string> origins = [publicOrigin.GetLeftPart(UriPartial.Authority)];
        if (builder.Environment.IsDevelopment())
            origins.Add("https://ainews.dev.localhost:8888");

        builder.Services.AddFido2(options =>
        {
            options.ServerDomain = publicOrigin.Host;
            options.ServerName = "AI News";
            options.Origins = origins;
        }).AddCachedMetadataService(config => config.AddFidoMetadataRepository());
    }

    public static void UseRequestSecurity(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(exceptionHandler => exceptionHandler.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank",
                    title = "An unexpected error occurred.",
                    status = StatusCodes.Status500InternalServerError,
                });
            }));
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.Use(ValidateAntiforgeryAndAddHeaders);
        app.UseAuthorization();

        app.MapGet("/api/auth/csrf", IssueAntiforgeryCookie).RequireAuthorization();
    }

    private static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions options,
        ConfigurationManager configuration,
        Uri publicOrigin,
        bool requireTrustedProxy)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost;
        options.ForwardedForHeaderName = GetForwardedForHeaderName(configuration);
        options.ForwardLimit = 1;
        options.AllowedHosts.Add(publicOrigin.Host);
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        bool hasTrustedProxy = false;

        foreach (string value in configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(value, out IPAddress? address))
                throw new InvalidOperationException($"Invalid trusted proxy address '{value}'.");
            options.KnownProxies.Add(address);
            hasTrustedProxy = true;
        }

        foreach (string value in configuration.GetSection("Security:TrustedProxyNetworks").Get<string[]>() ?? [])
        {
            if (!System.Net.IPNetwork.TryParse(value, out System.Net.IPNetwork network))
                throw new InvalidOperationException($"Invalid trusted proxy network '{value}'.");
            options.KnownIPNetworks.Add(network);
            hasTrustedProxy = true;
        }

        if (requireTrustedProxy && !hasTrustedProxy)
            throw new InvalidOperationException("At least one trusted reverse proxy address or network is required.");
        if (!hasTrustedProxy)
        {
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        }
    }

    private static string GetForwardedForHeaderName(ConfigurationManager configuration)
    {
        string? configured = configuration["Security:ForwardedForHeaderName"];
        if (string.IsNullOrWhiteSpace(configured)
            || string.Equals(configured, "X-Forwarded-For", StringComparison.OrdinalIgnoreCase))
        {
            return "X-Forwarded-For";
        }

        if (string.Equals(configured, "CF-Connecting-IP", StringComparison.OrdinalIgnoreCase))
            return "CF-Connecting-IP";

        throw new InvalidOperationException(
            "Security:ForwardedForHeaderName must be X-Forwarded-For or CF-Connecting-IP.");
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "AINews.Session";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.Events.OnValidatePrincipal = ValidateSession;
    }

    private static async Task ValidateSession(CookieValidatePrincipalContext context)
    {
        string? idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int userId))
        {
            context.RejectPrincipal();
            return;
        }

        UserContext db = context.HttpContext.RequestServices.GetRequiredService<UserContext>();
        User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        string? role = context.Principal?.FindFirstValue(ClaimTypes.Role);
        if (user is null || user.IsDisabled || role != user.Role.ToString())
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static async Task ValidateAntiforgeryAndAddHeaders(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        });

        bool unsafeAuthenticatedApiRequest = context.User.Identity?.IsAuthenticated == true
            && context.Request.Path.StartsWithSegments("/api")
            && !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method)
            && !context.Request.Path.StartsWithSegments("/api/auth/login")
            && !context.Request.Path.StartsWithSegments("/api/auth/register");
        if (unsafeAuthenticatedApiRequest)
        {
            try
            {
                IAntiforgery antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid antiforgery token.");
                return;
            }
        }

        await next(context);
    }

    private static IResult IssueAntiforgeryCookie(HttpContext context, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
        return Results.NoContent();
    }
}
