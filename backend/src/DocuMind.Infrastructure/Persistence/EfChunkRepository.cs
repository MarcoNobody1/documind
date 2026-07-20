using DocuMind.Application.Abstractions;
using DocuMind.Domain.Entities;

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
}
