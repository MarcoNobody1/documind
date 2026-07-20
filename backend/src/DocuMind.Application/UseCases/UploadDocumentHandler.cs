using DocuMind.Application.Abstractions;
using DocuMind.Domain.Entities;
using Microsoft.Extensions.AI;
using Pgvector;

namespace DocuMind.Application.UseCases;

/// <summary>
/// Orchestrates the document ingestion pipeline: extract text per page, chunk it, embed each
/// chunk, and persist the document with its chunks. If a document yields no extractable text
/// (e.g., a scanned/image-only PDF), no chunks are persisted and the result carries a warning
/// instead of silently reporting success.
/// </summary>
public class UploadDocumentHandler
{
    private const string EmptyTextWarning =
        "No extractable text was found in this PDF. It may be a scanned or image-only document; OCR is not supported.";

    private readonly ITextExtractor _textExtractor;
    private readonly IChunker _chunker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChunkRepository _chunkRepository;

    public UploadDocumentHandler(
        ITextExtractor textExtractor,
        IChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IChunkRepository chunkRepository)
    {
        _textExtractor = textExtractor;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _chunkRepository = chunkRepository;
    }

    public async Task<UploadResult> HandleAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var pages = await _textExtractor.ExtractAsync(content, cancellationToken);
        var pageCount = pages.Count;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            PageCount = pageCount,
            UploadedAtUtc = DateTime.UtcNow
        };

        var hasExtractableText = pages.Any(page => !string.IsNullOrWhiteSpace(page.Text));
        if (!hasExtractableText)
        {
            await _chunkRepository.AddDocumentAsync(document, Array.Empty<DocumentChunk>(), cancellationToken);
            return new UploadResult(document.Id, pageCount, 0, EmptyTextWarning);
        }

        var textChunks = _chunker.Chunk(pages);

        var embeddings = await _embeddingGenerator.GenerateAsync(
            textChunks.Select(chunk => chunk.Content),
            options: null,
            cancellationToken: cancellationToken);

        if (embeddings.Count != textChunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count ({embeddings.Count}) does not match chunk count ({textChunks.Count}).");
        }

        var chunks = textChunks
            .Zip(embeddings, (textChunk, embedding) => new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                PageNumber = textChunk.PageNumber,
                Ordinal = textChunk.Ordinal,
                Content = textChunk.Content,
                Embedding = new Vector(embedding.Vector)
            })
            .ToList();

        await _chunkRepository.AddDocumentAsync(document, chunks, cancellationToken);

        return new UploadResult(document.Id, pageCount, chunks.Count, Warning: null);
    }
}

/// <summary>
/// The outcome of an upload+ingestion pipeline run.
/// </summary>
/// <param name="DocumentId">The identifier of the created <see cref="Document"/>.</param>
/// <param name="PageCount">The number of pages extracted from the document.</param>
/// <param name="ChunkCount">The number of chunks persisted (0 if no text was extracted).</param>
/// <param name="Warning">A user-facing warning, e.g. when no extractable text was found; null on full success.</param>
public record UploadResult(Guid DocumentId, int PageCount, int ChunkCount, string? Warning);
