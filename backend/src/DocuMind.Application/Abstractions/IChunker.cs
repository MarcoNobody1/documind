namespace DocuMind.Application.Abstractions;

/// <summary>
/// Splits per-page extracted text into fixed-size, overlapping chunks suitable for embedding.
/// Implementations MUST preserve the source page number on every produced chunk.
/// </summary>
public interface IChunker
{
    IReadOnlyList<TextChunk> Chunk(IReadOnlyList<PageText> pages);
}

/// <summary>
/// A chunk of text ready for embedding, with its source page number and position within that
/// page preserved for exact citations.
/// </summary>
/// <param name="PageNumber">The 1-based page number this chunk was extracted from.</param>
/// <param name="Ordinal">The 0-based position of this chunk within its source page.</param>
/// <param name="Content">The chunk's text content.</param>
public record TextChunk(int PageNumber, int Ordinal, string Content);
