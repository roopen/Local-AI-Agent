using System.Net;
using System.Net.Sockets;
using System.Text;
using LocalAIAgent.Application.News;

// This service has internet access. The browser resides ONLY on the internal network.
// No cookies, credentials, content, URLs, or TLS plaintext are logged here.
TcpListener listener = new(IPAddress.Any, 3128);
listener.Start(128);
using SemaphoreSlim slots = new(128);
while (true)
{
    await slots.WaitAsync();
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleAsync(client).ContinueWith(_ => slots.Release(), TaskScheduler.Default);
}

static async Task HandleAsync(TcpClient client)
{
    using (client)
    using (CancellationTokenSource lifetime = new(TimeSpan.FromMinutes(3)))
    {
        NetworkStream incoming = client.GetStream();
        bool connected = false;
        try
        {
            using CancellationTokenSource headerDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            headerDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            List<byte> header = [];
            byte[] one = new byte[1];
            while (header.Count < 16384)
            {
                if (await incoming.ReadAsync(one, headerDeadline.Token) == 0) return;
                header.Add(one[0]);
                if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
            }
            if (header.Count >= 16384) throw new InvalidOperationException();
            string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
            string[] first = lines[0].Split(' ');
            if (first.Length != 3 || first[2] != "HTTP/1.1") throw new InvalidOperationException();
            bool tunnel = first[0] == "CONNECT";
            if (!tunnel && first[0] is not ("GET" or "HEAD")) throw new InvalidOperationException();
            if (!Uri.TryCreate(tunnel ? "https://" + first[1] : first[1], UriKind.Absolute, out Uri? target)
                || target.Scheme is not ("http" or "https") || target.Port is not (80 or 443)
                || target.UserInfo.Length > 0 || (!tunnel && target.Scheme != "http")) throw new InvalidOperationException();
            await using Stream outgoing = await PublicNetworkHttpHandler.ConnectPublicAsync(
                new DnsEndPoint(target.DnsSafeHost, target.Port), headerDeadline.Token);
            if (tunnel)
            {
                await incoming.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), lifetime.Token);
                connected = true;
            }
            else
            {
                // One request per connection; never let a pipelined absolute URL evade validation.
                if (lines.Any(line => line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException();
                string[] forwarded = lines.Skip(1).Where(line => line.Length > 0
                    && !line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)).ToArray();
                string rewritten = $"{first[0]} {target.PathAndQuery} HTTP/1.1\r\nHost: {target.Authority}\r\nConnection: close\r\n"
                    + string.Join("\r\n", forwarded) + "\r\n\r\n";
                await outgoing.WriteAsync(Encoding.ASCII.GetBytes(rewritten), lifetime.Token);
                connected = true;
                await outgoing.CopyToAsync(incoming, lifetime.Token);
                return;
            }
            Task upload = incoming.CopyToAsync(outgoing, lifetime.Token);
            Task download = outgoing.CopyToAsync(incoming, lifetime.Token);
            await Task.WhenAny(upload, download);
            await lifetime.CancelAsync();
            try { await Task.WhenAll(upload, download); } catch (OperationCanceledException) { }
        }
        catch (Exception)
        {
            if (!connected)
            {
                try { await incoming.WriteAsync("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray()); }
                catch (IOException) { }
            }
        }
    }
}
