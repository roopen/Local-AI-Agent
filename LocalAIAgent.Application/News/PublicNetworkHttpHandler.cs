using System.Net;
using System.Net.Sockets;

namespace LocalAIAgent.Application.News;

/// <summary>
/// Creates HTTP handlers that can connect only to publicly routable addresses.
/// The address check happens in ConnectCallback so DNS rebinding and redirect
/// targets cannot bypass the policy after an initial URL validation.
/// </summary>
internal static class PublicNetworkHttpHandler
{
    private static readonly IPNetwork[] NonPublicIpv4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("168.63.129.16/32"), // Azure platform virtual address
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
    ];

    private static readonly IPNetwork PublicIpv6Network = IPNetwork.Parse("2000::/3");

    private static readonly IPNetwork[] NonPublicIpv6Networks =
    [
        IPNetwork.Parse("2001::/23"),
        IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"),
        IPNetwork.Parse("3fff::/20"),
    ];

    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        UseCookies = false,
        // A process-level proxy would see only the proxy connection here and
        // could make the target request without enforcing this address policy.
        UseProxy = false,
        ConnectCallback = ConnectAsync,
    };

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !NonPublicIpv4Networks.Any(network => network.Contains(address)),
            AddressFamily.InterNetworkV6 => PublicIpv6Network.Contains(address)
                && !NonPublicIpv6Networks.Any(network => network.Contains(address)),
            _ => false,
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        DnsEndPoint endpoint = context.DnsEndPoint;
        IPAddress[] resolvedAddresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        IPAddress[] publicAddresses = [.. resolvedAddresses.Where(IsPublicAddress)];

        if (publicAddresses.Length == 0)
        {
            throw new HttpRequestException(
                "The feed host does not resolve to a publicly routable IP address.");
        }

        Exception? lastConnectionError = null;
        foreach (IPAddress address in publicAddresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                lastConnectionError = ex;
            }
        }

        throw new HttpRequestException("Unable to connect to the public feed host.", lastConnectionError);
    }
}
