using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace LocalAIAgent.API.Infrastructure;

public static class AuthRoles
{
    public const string Owner = "Owner";
    public const string Member = "Member";
}

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public static class InvitationTokens
{
    public static string Create()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool TryHash(string token, out string hash)
    {
        hash = string.Empty;
        if (token.Length != 43)
            return false;

        try
        {
            if (WebEncoders.Base64UrlDecode(token).Length != 32)
                return false;
        }
        catch (FormatException)
        {
            return false;
        }

        hash = Hash(token);
        return true;
    }
}

public sealed class BootstrapAccessPolicy(IConfiguration configuration)
{
    private readonly List<IPNetwork> _allowedNetworks = LoadNetworks(configuration);

    public bool IsAllowed(IPAddress? address)
    {
        if (address is null)
            return false;

        IPAddress candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _allowedNetworks.Any(network => network.Contains(candidate));
    }

    private static List<IPNetwork> LoadNetworks(IConfiguration configuration)
    {
        string[] configured = configuration.GetSection("Security:BootstrapAllowedNetworks").Get<string[]>() ?? [];
        string[] values = configured.Length > 0 ? configured : ["127.0.0.0/8", "::1/128"];
        List<IPNetwork> networks = [];

        foreach (string value in values)
        {
            if (!IPNetwork.TryParse(value, out IPNetwork network))
                throw new InvalidOperationException($"Invalid bootstrap network '{value}'. Use CIDR notation.");

            networks.Add(network);
        }

        return networks;
    }
}
