using Microsoft.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence;

/// <summary>
/// Asserts, on the application's own pooled connection, that the runtime prerequisites for
/// owner-scoped vector retrieval (ADR-H/ADR-I) actually hold — rather than merely documenting them.
/// </summary>
/// <remarks>
/// Two independent failures are checked, both of which would otherwise degrade
/// <c>EfChunkRepository.SearchAsync</c> into a silent under-return (fewer than the requested
/// <c>k</c> rows, with no error) instead of a startup fault:
/// <list type="bullet">
/// <item>The connection's <c>hnsw.iterative_scan</c> session setting must be <c>strict_order</c>.
/// This comes from the <c>Options=-c hnsw.iterative_scan=strict_order</c> fragment on
/// <c>ConnectionStrings:Postgres</c> (see <c>DependencyInjection.cs</c>) — a value that lives in
/// untracked user-secrets, so a fresh clone can silently omit it.</item>
/// <item>The installed <c>vector</c> extension must be &gt;= 0.8.0, the version iterative HNSW
/// scans shipped in. The Compose image tag (<c>pgvector/pgvector:pg17</c>) floats, so this is not
/// guaranteed to stay true over time even for an environment that was correctly configured once.
/// </item>
/// </list>
/// Run this once, in a scope after <c>app.Build()</c> and before <c>app.Run()</c> — not from
/// <c>AddInfrastructure</c>, whose documented invariant is that DI registration never contacts the
/// database, and not before <c>Build()</c>, where EF's design-time tooling (<c>dotnet ef database
/// update</c>) would execute it against a schema that may not exist yet.
/// </remarks>
public static class RetrievalPrerequisiteCheck
{
    /// <summary>
    /// The first pgvector version to support iterative HNSW scans.
    /// </summary>
    private static readonly int[] MinimumSupportedVectorVersion = [0, 8, 0];

    public static async Task VerifyAsync(DocuMindDbContext dbContext, CancellationToken cancellationToken = default)
    {
        // `current_setting(name, missing_ok => true)` returns NULL for an unset/unregistered GUC
        // instead of raising `unrecognized configuration parameter` the way `SHOW` would — a
        // `SHOW`-raised error is indistinguishable from a genuine connection fault at the catch
        // site, while NULL is a clean "absent" result this method can report on directly.
        //
        // `extversion` is compared as an integer array, not as text: `'0.10.0' < '0.8.0'` lexically,
        // so a naive string comparison would reject a newer version once the extension reaches
        // 0.10.x. `string_to_array(extversion, '.')::int[]` compares element-wise instead.
        var row = await dbContext.Database
            .SqlQueryRaw<PrerequisiteRow>(
                """
                SELECT
                    current_setting('hnsw.iterative_scan', true) AS "IterativeScan",
                    (SELECT extversion FROM pg_extension WHERE extname = 'vector') AS "VectorExtensionVersion"
                """)
            .SingleAsync(cancellationToken);

        var installedVersion = ParseVersion(row.VectorExtensionVersion);

        if (installedVersion is null || CompareVersions(installedVersion, MinimumSupportedVectorVersion) < 0)
        {
            throw new InvalidOperationException(
                $"The installed pgvector extension ({row.VectorExtensionVersion ?? "not installed"}) does "
                + $"not support iterative HNSW scans, which require pgvector >= "
                + $"{string.Join('.', MinimumSupportedVectorVersion)}. Owner-scoped retrieval (Phase 2, "
                + "ADR-H/ADR-I) depends on iterative scan to avoid silently returning fewer results than "
                + "requested. The 'pgvector/pgvector:pg17' image tag in docker-compose.yml floats, so this "
                + "can regress even in a previously-working environment — pin or upgrade to a tag bundling "
                + "pgvector >= 0.8.0, or run `ALTER EXTENSION vector UPDATE;` against the target database.");
        }

        if (row.IterativeScan != "strict_order")
        {
            throw new InvalidOperationException(
                "The database connection is missing the required 'hnsw.iterative_scan = strict_order' "
                + "session setting (ADR-I). Add 'Options=-c hnsw.iterative_scan=strict_order' to the "
                + "'ConnectionStrings:Postgres' value in user-secrets — see backend/src/DocuMind."
                + "Infrastructure/DependencyInjection.cs and the 'Getting started' section of README.md. "
                + "Without it, owner-scoped retrieval (EfChunkRepository.SearchAsync) can silently return "
                + "fewer than the requested number of results instead of a fault, once enough of another "
                + "owner's chunks rank ahead of the caller's.");
        }
    }

    private static int[]? ParseVersion(string? extversion)
    {
        if (string.IsNullOrWhiteSpace(extversion))
        {
            return null;
        }

        var parts = extversion.Split('.');
        var version = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out version[i]))
            {
                return null;
            }
        }

        return version;
    }

    private static int CompareVersions(int[] left, int[] right)
    {
        var length = Math.Max(left.Length, right.Length);

        for (var i = 0; i < length; i++)
        {
            var leftPart = i < left.Length ? left[i] : 0;
            var rightPart = i < right.Length ? right[i] : 0;

            var comparison = leftPart.CompareTo(rightPart);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private sealed class PrerequisiteRow
    {
        public string? IterativeScan { get; set; }

        public string? VectorExtensionVersion { get; set; }
    }
}
