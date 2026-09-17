using System.Diagnostics;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LocalAIAgent.API.Infrastructure;

// Registered only in Development. Provision at application startup, never during a feed/reader request.
internal sealed class ArticleReaderDevelopmentStartup(IConfiguration configuration, IWebHostEnvironment environment,
    ILogger<ArticleReaderDevelopmentStartup> logger) : IHostedService
{
    private static readonly SemaphoreSlim StartupLock = new(1, 1);
    private bool registeredLifetime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsManagedEndpoint(configuration["ArticleReader:McpEndpoint"]))
        {
            logger.LogInformation("Article reader automatic startup skipped: the MCP endpoint is not the local development endpoint.");
            return;
        }
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            await StartupLock.WaitAsync(timeout.Token);
            try { await EnsureReadyAsync(timeout.Token); }
            finally { StartupLock.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // A missing optional reader dependency must not prevent the rest of the app from starting.
            logger.LogError(ex, "Article reader development startup failed. The feed remains available. " +
                "Check the container engine and run 'podman compose -p ainews-reader -f deploy/article-reader/compose.yaml up --build -d' from the repository root, then retry. " +
                "Set ArticleReader:AutoStartLocalServices=false to manage services manually.");
        }
    }

    internal static bool IsManagedEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) && uri.IsLoopback
        && uri.Scheme == "http" && uri.Port == 8931 && uri.AbsolutePath == "/mcp"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    internal async Task EnsureReadyAsync(CancellationToken ct)
    {
        string engine = configuration["ArticleReader:ContainerEngine"] ?? "podman";
        if (engine is not ("podman" or "docker")) throw new InvalidOperationException("ArticleReader:ContainerEngine must be podman or docker.");
        string compose = FindComposeFile(environment.ContentRootPath);
        using FileStream? lifetimeLock = engine == "podman" && OperatingSystem.IsWindows()
            ? await ArticleReaderDebugLifetime.LockAsync(ct) : null;
        ArticleReaderDebugLifetime.State? lifetime = null;
        if (lifetimeLock is not null)
        {
            lifetime = ArticleReaderDebugLifetime.Load();
            lifetime.Sessions.RemoveAll(session => !ArticleReaderDebugLifetime.IsAlive(session));
            var session = ArticleReaderDebugLifetime.CurrentSession();
            if (!lifetime.Sessions.Contains(session)) lifetime.Sessions.Add(session);
            ArticleReaderDebugLifetime.Save(lifetime);
            registeredLifetime = true;
            if (lifetime.Machine is not null) ArticleReaderDebugLifetime.StartWatcher();
        }
        logger.LogInformation("Starting local article reader dependencies with {Engine}. The first run may take several minutes to build the browser image.", engine);
        ProcessResult info = await RunAsync(engine, ["info"], ct);
        if (info.ExitCode != 0)
        {
            if (engine != "podman" || !OperatingSystem.IsWindows())
                throw new InvalidOperationException($"Start {engine} before debugging the API. {info.Output}");
            logger.LogInformation("Starting the Podman virtual machine for the article reader.");
            ProcessResult machines = await RunAsync(engine, ["machine", "list", "--format", "json"], ct);
            if (machines.ExitCode != 0) throw new InvalidOperationException("Could not inspect Podman machines: " + machines.Output);
            using var machineList = System.Text.Json.JsonDocument.Parse(machines.Output);
            var machine = machineList.RootElement.EnumerateArray().FirstOrDefault(item => item.GetProperty("Default").GetBoolean());
            if (machine.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                throw new InvalidOperationException("No default Podman machine exists. Run 'podman machine init' once.");
            string machineName = machine.GetProperty("Name").GetString()!;
            bool claimedMachine = false;
            if (!machine.GetProperty("Running").GetBoolean() && !machine.GetProperty("Starting").GetBoolean())
            {
                claimedMachine = true;
                lifetime!.Machine = machineName;
                ArticleReaderDebugLifetime.Save(lifetime);
                // Arm cleanup before starting the VM so cancellation during startup is also covered.
                ArticleReaderDebugLifetime.StartWatcher();
            }
            ProcessResult start = await RunAsync(engine, ["machine", "start", machineName], ct);
            if (start.ExitCode != 0 && claimedMachine)
            {
                lifetime!.Machine = null;
                ArticleReaderDebugLifetime.Save(lifetime);
            }
            // Another debug instance may have started it between info and machine start.
            if (start.ExitCode != 0 && (await RunAsync(engine, ["info"], ct)).ExitCode != 0)
                throw new InvalidOperationException($"Podman machine could not start. If no machine exists, run 'podman machine init' once. {start.Output}");
        }
        bool needsBuild = (await RunAsync(engine, ["image", "inspect", "localhost/ainews-article-mcp:0.0.80"], ct)).ExitCode != 0
            || (await RunAsync(engine, ["image", "inspect", "localhost/ainews-article-egress:1"], ct)).ExitCode != 0;
        ProcessResult up = await RunAsync(engine, ["compose", "-p", "ainews-reader", "-f", compose, "up", needsBuild ? "--build" : "--no-build", "-d"], ct);
        if (up.ExitCode != 0) throw new InvalidOperationException($"Article reader Compose startup failed (exit {up.ExitCode}). {up.Output}");

        // Probe the real protocol and launch Chromium, not just a listening TCP socket.
        Exception? lastError = null;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ProbeAsync(ct);
                logger.LogInformation("Article reader ready: MCP handshake and isolated Chromium launch succeeded at {Endpoint}.",
                    configuration["ArticleReader:McpEndpoint"]);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { lastError = ex; }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        throw new InvalidOperationException("Reader containers started, but MCP/Chromium is not ready. Inspect 'podman compose -p ainews-reader -f deploy/article-reader/compose.yaml logs --tail 50'.", lastError);
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using McpClient client = await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri(configuration["ArticleReader:McpEndpoint"]!),
            TransportMode = HttpTransportMode.StreamableHttp, ConnectionTimeout = TimeSpan.FromSeconds(3),
        }), cancellationToken: timeout.Token);
        try
        {
            CallToolResult result = await client.CallToolAsync("browser_navigate", new Dictionary<string, object?> { ["url"] = "about:blank" }, cancellationToken: timeout.Token);
            if (result.IsError == true)
                throw new InvalidOperationException("Chromium startup failed: " + string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text)));
        }
        finally
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
            await client.CallToolAsync("browser_close", cancellationToken: cleanup.Token);
        }
    }

    private static string FindComposeFile(string contentRoot)
    {
        for (DirectoryInfo? directory = new(contentRoot); directory is not null; directory = directory.Parent)
        {
            string file = Path.Combine(directory.FullName, "deploy", "article-reader", "compose.yaml");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Cannot find deploy/article-reader/compose.yaml. Start the development API from the repository checkout.");
    }

    internal sealed record ProcessResult(int ExitCode, string Output);

    internal static async Task<ProcessResult> RunAsync(string engine, string[] arguments, CancellationToken ct)
    {
        ProcessStartInfo start = new(engine) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {engine}. Ensure it is installed and on PATH, then restart Visual Studio.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> stderr = process.StandardError.ReadToEndAsync(ct);
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        string output = (await stdout) + "\n" + (await stderr);
        return new(process.ExitCode, output[^Math.Min(output.Length, 8000)..]);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (registeredLifetime) await ArticleReaderDebugLifetime.ReleaseAsync(cancellationToken);
    }
}
