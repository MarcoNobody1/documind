using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace DocuMind.IntegrationTests;

/// <summary>
/// The primary security gate for Phase 2 PR4 (ADR-K). This is a security assertion, not a
/// performance check: it proves — on every run, not just once in a manual demo — that user A can
/// never retrieve user B's chunks, even when they are the best semantic match, and that the
/// production query plan actually uses the HNSW index rather than silently falling back to a
/// sequential scan (the exact "compiles, runs, silently wrong plan" failure class this project has
/// already been bitten by).
/// </summary>
/// <remarks>
/// Embeddings are analytic, not random (ADR-K): the query vector is <c>q = [1, 0, ..., 0]</c>, and
/// chunk <c>i</c> is placed at <c>[cos θᵢ, sin θᵢ, 0, ...]</c>, so pgvector's cosine distance
/// (<c>&lt;=&gt;</c>) is exactly <c>1 - cos θᵢ</c> and the global ranking by distance is
/// arithmetically known in advance — not just plausible-looking. θ is assigned in strictly
/// increasing order across three blocks, in this fixed sequence: 39 chunks planted onto user B
/// (closer to the query than anything user A owns), then every one of user A's chunks (so user A's
/// nearest chunk lands at global rank 40 — "~40th globally", per ADR-K), then the remainder of user
/// B's and all of user C's chunks. If the owner filter were ever silently dropped, an unfiltered
/// top-5 query for user A would return user B's planted chunks instead — the exact leak this test
/// exists to catch.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class OwnerIsolationTests : IAsyncLifetime
{
    private const int DocumentsPerUser = 3;
    private const int ChunksPerDocument = 560; // 3 users x 3 docs x 560 = ~5,040 chunks total.
    private const int PlantedCloserChunks = 39; // User B chunks ranked ahead of ALL of user A's.
    private const int TopK = 5;

    private readonly PostgresFixture _fixture;
    private DocuMindDbContext _dbContext = null!;
    private CapturingCommandInterceptor _interceptor = null!;

    private Guid _userAId;
    private List<Guid> _predictedTopKChunkIdsInOrder = [];
    private HashSet<Guid> _userAChunkIds = [];
    private HashSet<string> _userADocumentNames = [];

    public OwnerIsolationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _interceptor = new CapturingCommandInterceptor();
        _dbContext = _fixture.CreateDbContext(_interceptor);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task SearchAsync_UsesHnswIndexScan_ReturnsExactlyKOwnedChunks_NoForeignOwnerRows()
    {
        var repository = new EfChunkRepository(_dbContext);
        var queryEmbedding = new ReadOnlyMemory<float>(BuildEmbeddingComponents(0));

        var results = await repository.SearchAsync(_userAId, queryEmbedding, TopK, CancellationToken.None);

        // Re-issue EF's OWN generated SQL under EXPLAIN ANALYZE, with the same bound parameters, on
        // a connection from the same data source — the plan asserted below is the plan the
        // application actually emits, not a hand-written approximation (ADR-K). If this ever
        // flakes, the fix is to raise the seeded row count above — never `SET enable_seqscan =
        // off`, which would remove the very planner decision this test measures.
        var plan = await ExplainLastQueryAsync();

        Assert.Contains("Index Scan using \"IX_document_chunks_Embedding\"", plan);

        // Scoped to document_chunks on purpose: "Seq Scan on documents" at 9 rows is legitimate
        // planner behaviour at that row count, and a blanket "no Seq Scan anywhere" assertion would
        // fail spuriously on that unrelated table.
        Assert.DoesNotContain("Seq Scan on document_chunks", plan);

        // (ii) Exactly k, not silently fewer — the spec's "no silent under-return" requirement.
        Assert.Equal(TopK, results.Count);
        Assert.Equal(_predictedTopKChunkIdsInOrder, results.Select(chunk => chunk.ChunkId).ToList());

        // (iii) Zero rows belong to another owner.
        Assert.All(results, chunk => Assert.Contains(chunk.ChunkId, _userAChunkIds));
        Assert.All(results, chunk => Assert.Contains(chunk.DocumentName, _userADocumentNames));
    }

    private async Task<string> ExplainLastQueryAsync()
    {
        Assert.False(string.IsNullOrEmpty(_interceptor.LastCommandText), "SearchAsync did not issue a captured query.");

        await _dbContext.Database.OpenConnectionAsync();
        try
        {
            var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();

            await using var explainCommand = connection.CreateCommand();
            explainCommand.CommandText = $"EXPLAIN (ANALYZE, FORMAT TEXT) {_interceptor.LastCommandText}";

            foreach (var parameter in _interceptor.LastParameters)
            {
                explainCommand.Parameters.Add((NpgsqlParameter)parameter.Clone());
            }

            var planLines = new List<string>();
            await using var reader = await explainCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planLines.Add(reader.GetString(0));
            }

            return string.Join('\n', planLines);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task SeedAsync()
    {
        _userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var userCId = Guid.NewGuid();

        _dbContext.Users.AddRange(
            NewUser(_userAId, "owner-isolation-a@test.documind"),
            NewUser(userBId, "owner-isolation-b@test.documind"),
            NewUser(userCId, "owner-isolation-c@test.documind"));
        await _dbContext.SaveChangesAsync();

        var userADocuments = NewDocuments(_userAId, "a");
        var userBDocuments = NewDocuments(userBId, "b");
        var userCDocuments = NewDocuments(userCId, "c");

        _dbContext.Documents.AddRange(userADocuments.Concat(userBDocuments).Concat(userCDocuments));
        await _dbContext.SaveChangesAsync();

        _userADocumentNames = userADocuments.Select(document => document.FileName).ToHashSet();

        var totalChunks = PlantedCloserChunks + (2 * DocumentsPerUser * ChunksPerDocument);
        var thetaStep = (Math.PI / 2) / (totalChunks + 1);
        var thetaIndex = 0;

        _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        // Block 1: 39 chunks planted onto user B, strictly closer to the query than every one of
        // user A's chunks below. An unfiltered search for user A would return these instead of
        // user A's own content — the exact leak this test exists to prove cannot happen.
        await InsertChunksAsync(userBDocuments, PlantedCloserChunks, () => thetaStep * ++thetaIndex);

        // Block 2: every one of user A's chunks, immediately after block 1 — so user A's smallest
        // theta lands at global rank PlantedCloserChunks + 1 ("~40th", per ADR-K), and user A's own
        // nearest TopK chunks are exactly the first TopK generated here, in this exact order.
        var userAChunkIdsInOrder = new List<Guid>();
        var userATotalChunks = DocumentsPerUser * ChunksPerDocument;
        await InsertChunksAsync(
            userADocuments,
            userATotalChunks,
            () => thetaStep * ++thetaIndex,
            onInserted: userAChunkIdsInOrder.Add);
        _predictedTopKChunkIdsInOrder = userAChunkIdsInOrder.Take(TopK).ToList();
        _userAChunkIds = userAChunkIdsInOrder.ToHashSet();

        // Block 3: the remainder of user B's chunks and all of user C's chunks — placed after user
        // A's block, so their relative ordering among themselves cannot be mistaken for user A's
        // nearest results.
        var remainingUserBChunks = (DocumentsPerUser * ChunksPerDocument) - PlantedCloserChunks;
        await InsertChunksAsync(userBDocuments, remainingUserBChunks, () => thetaStep * ++thetaIndex);
        await InsertChunksAsync(userCDocuments, DocumentsPerUser * ChunksPerDocument, () => thetaStep * ++thetaIndex);

        _dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        // Without this, the planner has no fresh statistics for a table that went from empty to
        // ~5,000 rows in this same connection and may not consider the HNSW index at all.
        await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE documents; ANALYZE document_chunks;");
    }

    private async Task InsertChunksAsync(
        List<Document> ownerDocuments, int count, Func<double> nextTheta, Action<Guid>? onInserted = null)
    {
        const int batchSize = 500;
        var pending = new List<DocumentChunk>(batchSize);

        for (var i = 0; i < count; i++)
        {
            var document = ownerDocuments[i % ownerDocuments.Count];
            var theta = nextTheta();
            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                OwnerId = document.OwnerId,
                PageNumber = 1,
                Ordinal = i,
                Content = $"Synthetic owner-isolation chunk {i} (theta={theta:F6}).",
                Embedding = new Vector(BuildEmbeddingComponents(theta))
            };

            pending.Add(chunk);
            onInserted?.Invoke(chunk.Id);

            if (pending.Count == batchSize)
            {
                await FlushAsync(pending);
            }
        }

        if (pending.Count > 0)
        {
            await FlushAsync(pending);
        }
    }

    private async Task FlushAsync(List<DocumentChunk> pending)
    {
        _dbContext.DocumentChunks.AddRange(pending);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        pending.Clear();
    }

    private static float[] BuildEmbeddingComponents(double theta)
    {
        var values = new float[DocuMindDbContext.EmbeddingDimensions];
        values[0] = (float)Math.Cos(theta);
        values[1] = (float)Math.Sin(theta);
        return values;
    }

    private static ApplicationUser NewUser(Guid id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
    };

    private static List<Document> NewDocuments(Guid ownerId, string ownerLabel) =>
        Enumerable.Range(1, DocumentsPerUser)
            .Select(n => new Document
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                FileName = $"owner-isolation-{ownerLabel}-{n}.pdf",
                PageCount = 1,
                UploadedAtUtc = DateTime.UtcNow
            })
            .ToList();
}
