using DocuMind.Application.Abstractions;
using Microsoft.ML.Tokenizers;

namespace DocuMind.Infrastructure.Text;

/// <summary>
/// Splits per-page text into fixed-size, overlapping chunks of approximately
/// <see cref="ChunkSizeTokens"/> tokens with <see cref="OverlapRatio"/> overlap between adjacent
/// chunks, using a tiktoken-compatible tokenizer aligned with the configured embedding model.
/// Each produced chunk keeps the page number it was extracted from.
/// </summary>
public class FixedSizeChunker : IChunker
{
    /// <summary>Target chunk size, in tokens.</summary>
    public const int ChunkSizeTokens = 800;

    /// <summary>Fraction of a chunk that overlaps with the next chunk.</summary>
    public const double OverlapRatio = 0.15;

    private readonly TiktokenTokenizer _tokenizer;

    public FixedSizeChunker()
    {
        // Aligned with the text-embedding-3-small deployment (cl100k_base encoding).
        _tokenizer = TiktokenTokenizer.CreateForModel("text-embedding-3-small");
    }

    public IReadOnlyList<TextChunk> Chunk(IReadOnlyList<PageText> pages)
    {
        var overlapTokens = (int)(ChunkSizeTokens * OverlapRatio);
        var step = ChunkSizeTokens - overlapTokens;

        var chunks = new List<TextChunk>();

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            var tokenIds = _tokenizer.EncodeToIds(page.Text);
            if (tokenIds.Count == 0)
            {
                continue;
            }

            var ordinal = 0;
            for (var start = 0; start < tokenIds.Count; start += step)
            {
                var length = Math.Min(ChunkSizeTokens, tokenIds.Count - start);
                var windowIds = tokenIds.Skip(start).Take(length).ToList();
                var text = _tokenizer.Decode(windowIds);

                chunks.Add(new TextChunk(page.PageNumber, ordinal, text));
                ordinal++;

                if (start + length >= tokenIds.Count)
                {
                    break;
                }
            }
        }

        return chunks;
    }
}
