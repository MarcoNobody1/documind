using DocuMind.Application.Abstractions;
using DocuMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IChunkRepository"/>.
/// </summary>
public class EfChunkRepository : IChunkRepository
{
    private readonly DocuMindDbContext _dbContext;

    public EfChunkRepository(DocuMindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddDocumentAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Add(document);

        if (chunks.Count > 0)
        {
            _dbContext.DocumentChunks.AddRange(chunks);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(queryEmbedding);

        // `CosineDistance` (Pgvector.EntityFrameworkCore) translates to PostgreSQL's `<=>`
        // operator, which matches the HNSW index's `vector_cosine_ops` operator class in
        // DocuMindDbContext. Ordering by any other distance function (e.g. `L2Distance`, which
        // maps to `<->`) would compile and run, but PostgreSQL silently ignores the index and
        // falls back to a sequential scan — no error, no warning. Do not change this operator
        // without also changing the index.
        // `CosineDistance(queryVector)` is evaluated twice — once for `OrderBy`, once in the
        // projection to populate `RetrievedChunk.Distance` — so PostgreSQL recomputes the
        // distance for the `k` rows that already survived the ORDER BY + LIMIT (5 extra
        // computations at the default k=5, not 5 extra index scans). Deliberately left as-is
        // (sdd-verify SUGGESTION 3): avoiding it needs an intermediate anonymous-type projection
        // that carries the already-computed distance through the `Join`, which makes this query
        // materially less readable for an unmeasurable gain at this scale. Revisit only if
        // profiling ever shows this query as a real cost, not preemptively.
        return await _dbContext.DocumentChunks
            .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
            .Take(k)
            .Join(
                _dbContext.Documents,
                chunk => chunk.DocumentId,
                document => document.Id,
                (chunk, document) => new RetrievedChunk(
                    chunk.Id,
                    document.FileName,
                    chunk.PageNumber,
                    chunk.Content,
                    chunk.Embedding.CosineDistance(queryVector)))
            .ToListAsync(cancellationToken);
    }
}
