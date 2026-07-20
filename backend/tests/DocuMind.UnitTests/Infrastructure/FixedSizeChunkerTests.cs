using System.Text.RegularExpressions;
using DocuMind.Application.Abstractions;
using DocuMind.Infrastructure.Text;
using Microsoft.ML.Tokenizers;

namespace DocuMind.UnitTests.Infrastructure;

public class FixedSizeChunkerTests
{
    private static readonly TiktokenTokenizer Tokenizer = TiktokenTokenizer.CreateForModel("text-embedding-3-small");

    [Fact]
    public void Chunk_LongPage_SplitsIntoMultipleChunksWithinTargetSize()
    {
        var longText = string.Join(" ", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var pages = new List<PageText> { new(PageNumber: 3, Text: longText) };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1, "Expected a long page to be split into multiple chunks.");

        foreach (var chunk in chunks)
        {
            Assert.Equal(3, chunk.PageNumber);
            var tokenCount = Tokenizer.CountTokens(chunk.Content);
            Assert.True(tokenCount <= FixedSizeChunker.ChunkSizeTokens,
                $"Chunk had {tokenCount} tokens, expected at most {FixedSizeChunker.ChunkSizeTokens}.");
        }
    }

    [Fact]
    public void Chunk_LongPage_AssignsSequentialOrdinalsStartingAtZero()
    {
        var longText = string.Join(" ", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var pages = new List<PageText> { new(PageNumber: 1, Text: longText) };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);

        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Ordinal);
        }
    }

    [Fact]
    public void Chunk_AdjacentChunks_ShareOverlappingContent()
    {
        var longText = string.Join(" ", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var pages = new List<PageText> { new(PageNumber: 1, Text: longText) };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);
        Assert.True(chunks.Count >= 2, "Need at least two chunks to assert overlap.");

        var firstWords = Regex.Matches(chunks[0].Content, @"word\d+").Select(m => m.Value).ToList();
        var secondWords = Regex.Matches(chunks[1].Content, @"word\d+").Select(m => m.Value).ToList();

        var overlap = firstWords.Intersect(secondWords).ToList();

        Assert.NotEmpty(overlap);
        Assert.NotEqual(chunks[0].Content, chunks[1].Content);
    }

    [Fact]
    public void Chunk_ShortPage_ProducesSingleChunkPreservingPageNumber()
    {
        var pages = new List<PageText> { new(PageNumber: 7, Text: "A short paragraph of text.") };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);

        var chunk = Assert.Single(chunks);
        Assert.Equal(7, chunk.PageNumber);
        Assert.Equal(0, chunk.Ordinal);
    }

    [Fact]
    public void Chunk_EmptyOrWhitespacePage_ProducesNoChunks()
    {
        var pages = new List<PageText> { new(PageNumber: 1, Text: "   ") };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);

        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_MultiplePages_PreservesEachPageNumber()
    {
        var pages = new List<PageText>
        {
            new(PageNumber: 1, Text: "Page one content."),
            new(PageNumber: 2, Text: "Page two content.")
        };
        var chunker = new FixedSizeChunker();

        var chunks = chunker.Chunk(pages);

        Assert.Equal([1, 2], chunks.Select(c => c.PageNumber));
    }
}
