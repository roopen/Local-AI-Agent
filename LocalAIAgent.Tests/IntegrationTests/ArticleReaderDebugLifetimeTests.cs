using LocalAIAgent.API.Infrastructure;

namespace LocalAIAgent.Tests.IntegrationTests;

public class ArticleReaderDebugLifetimeTests
{
    [Fact]
    public void PreexistingMachineIsNeverStopped()
    {
        ArticleReaderDebugLifetime.State state = new() { Machine = null };
        Assert.False(ArticleReaderDebugLifetime.ShouldStop(state, _ => false));
    }

    [Fact]
    public void DebugOwnedMachineStopsOnlyAfterLastSessionEnds()
    {
        ArticleReaderDebugLifetime.Lease first = new(1, 100);
        ArticleReaderDebugLifetime.Lease second = new(2, 200);
        ArticleReaderDebugLifetime.State state = new() { Machine = "debug-machine", Sessions = [first, second] };
        Assert.False(ArticleReaderDebugLifetime.ShouldStop(state, session => session == second));
        Assert.True(ArticleReaderDebugLifetime.ShouldStop(state, _ => false));
    }

    [Fact]
    public void ProcessIdentityIncludesStartTimeToAvoidPidReuse()
    {
        ArticleReaderDebugLifetime.Lease current = ArticleReaderDebugLifetime.CurrentSession();
        Assert.True(ArticleReaderDebugLifetime.IsAlive(current));
        Assert.False(ArticleReaderDebugLifetime.IsAlive(current with { StartedUtcTicks = current.StartedUtcTicks - 1 }));
        Assert.False(ArticleReaderDebugLifetime.IsAlive(new(int.MaxValue, 0)));
    }
}
