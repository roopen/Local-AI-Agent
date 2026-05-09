using LocalAIAgent.API.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Tests.TestInfrastructure;

/// <summary>
/// xUnit fixture base for tests that need a real <see cref="UserContext"/>
/// backed by an in-memory SQLite DB. Each test class instance gets its own
/// connection, so tests don't see each other's data.
/// </summary>
public abstract class InMemoryDbTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    protected UserContext Db { get; }

    protected InMemoryDbTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // UserContext exposes DbSets as `required` — DI-based construction populates them
        // via reflection in the DbContext base ctor, dodging the compiler's required-member check.
        ServiceCollection services = new();
        services.AddDbContext<UserContext>(o => o.UseSqlite(_connection));
        _services = services.BuildServiceProvider();

        Db = _services.GetRequiredService<UserContext>();
        Db.Database.EnsureCreated();
    }

    public virtual void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
