using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DocuMind.IntegrationTests;

/// <summary>
/// One pgvector-backed Postgres container, shared for the whole test assembly (xUnit collection
/// fixture — see <see cref="PostgresCollection"/>).
/// </summary>
/// <remarks>
/// Uses the same image tag as <c>docker-compose.yml</c> (<c>pgvector/pgvector:pg17</c>) on purpose:
/// the floating-tag risk this project accepted for production (ADR-I/ADR-K) is shared with CI
/// rather than diverged from it, and <see cref="DocuMind.Infrastructure.Persistence.RetrievalPrerequisiteCheck"/>'s
/// version assertion — exercised indirectly by every test in this assembly via the real
/// <c>EfChunkRepository</c> — turns that risk into a gate that runs on every commit instead of
/// something discovered manually.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    /// <summary>
    /// The connection string, with <c>Options=-c hnsw.iterative_scan=strict_order</c> appended.
    /// This is ADR-I's reference implementation: a tracked-code guarantee that this exact fragment
    /// is exercised on every CI run, independent of whatever a fresh clone's untracked
    /// user-secrets store does or does not contain for the real application connection string.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = $"{_container.GetConnectionString()};Options=-c hnsw.iterative_scan=strict_order";

        // Exercises the truncate + NOT NULL + composite-FK path in AddDocumentOwnership as a side
        // effect of applying every migration from scratch — the same path a fresh clone's
        // `dotnet ef database update` takes.
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Builds a <see cref="DocuMindDbContext"/> against this fixture's container, configured
    /// exactly like the real composition root (<c>DependencyInjection.AddInfrastructure</c>):
    /// Npgsql + <c>UseVector()</c>. Optional interceptors let a test capture the exact SQL/
    /// parameters EF issues (see <see cref="CapturingCommandInterceptor"/>).
    /// </summary>
    public DocuMindDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocuMindDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector());

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return new DocuMindDbContext(optionsBuilder.Options);
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
