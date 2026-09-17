using System.Diagnostics;
using System.Text.Json;

namespace LocalAIAgent.API.Infrastructure;

// A separate process survives termination of the debuggee. A shared lease protects overlapping F5 sessions.
internal static class ArticleReaderDebugLifetime
{
    internal sealed record Lease(int ProcessId, long StartedUtcTicks);
    internal sealed class State
    {
        public string? Machine { get; set; }
        public List<Lease> Sessions { get; set; } = [];
    }

    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalAIAgent", "reader-debug");

    internal static async Task<FileStream> LockAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(DirectoryPath);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(DirectoryPath, "lifetime.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(200, ct); }
        }
    }

    internal static State Load()
    {
        string path = Path.Combine(DirectoryPath, "lifetime.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? new() : new();
    }

    internal static void Save(State state)
    {
        string path = Path.Combine(DirectoryPath, "lifetime.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    internal static Lease CurrentSession()
    {
        using Process process = Process.GetCurrentProcess();
        return new(process.Id, process.StartTime.ToUniversalTime().Ticks);
    }

    internal static bool IsAlive(Lease lease)
    {
        try
        {
            using Process process = Process.GetProcessById(lease.ProcessId);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == lease.StartedUtcTicks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        // If process inspection is denied, err on the side of leaving a potentially active session alone.
        catch (System.ComponentModel.Win32Exception) { return true; }
    }

    internal static bool ShouldStop(State state, Func<Lease, bool> isAlive) =>
        state.Machine is not null && !state.Sessions.Any(isAlive);

    internal static void StartWatcher()
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the debug application executable.");
        string assemblyName = typeof(ArticleReaderDebugLifetime).Assembly.GetName().Name!;
        if (!Path.GetFileNameWithoutExtension(executable).Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
            executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        ProcessStartInfo start = new(executable) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(ArticleReaderDebugLifetime).Assembly.Location);
        start.ArgumentList.Add("--article-reader-debug-watchdog");
        using Process watcher = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the debug cleanup watcher.");
    }

    internal static async Task ReleaseAsync(CancellationToken ct)
    {
        using FileStream gate = await LockAsync(ct);
        State state = Load();
        Lease current = CurrentSession();
        state.Sessions.RemoveAll(session => session == current || !IsAlive(session));
        Save(state);
        // The watchdog performs shutdown, including when Visual Studio skips StopAsync.
    }

    internal static async Task WatchAsync()
    {
        try
        {
            while (true)
            {
                using (FileStream gate = await LockAsync(CancellationToken.None))
                {
                    State state = Load();
                    if (state.Machine is null) return;
                    if (ShouldStop(state, IsAlive))
                    {
                        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
                        // The lock prevents a new debugger from starting/reusing the VM during shutdown.
                        await StopMachineAsync(state.Machine, timeout.Token);
                        state.Machine = null;
                        state.Sessions.Clear();
                        Save(state);
                        return;
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(DirectoryPath);
            await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "cleanup-error.log"), $"{DateTimeOffset.UtcNow:O}\n{ex}");
        }
    }

    private static async Task StopMachineAsync(string name, CancellationToken ct)
    {
        // A debugger can be killed while 'machine start' is still finishing in its child process.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        while (true)
        {
            var inspect = await ArticleReaderDevelopmentStartup.RunAsync("podman", ["machine", "list", "--format", "json"], ct);
            if (inspect.ExitCode != 0) throw new InvalidOperationException("Cannot inspect Podman during debug cleanup: " + inspect.Output);
            using JsonDocument machines = JsonDocument.Parse(inspect.Output);
            JsonElement machine = machines.RootElement.EnumerateArray().FirstOrDefault(item => item.GetProperty("Name").GetString() == name);
            if (machine.ValueKind == JsonValueKind.Undefined) return;
            if (machine.GetProperty("Starting").GetBoolean())
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }
            if (!machine.GetProperty("Running").GetBoolean()) return;
            var stop = await ArticleReaderDevelopmentStartup.RunAsync("podman", ["machine", "stop", name], ct);
            if (stop.ExitCode != 0) throw new InvalidOperationException($"Could not stop debug-owned Podman machine {name}: {stop.Output}");
            return;
        }
    }
}
