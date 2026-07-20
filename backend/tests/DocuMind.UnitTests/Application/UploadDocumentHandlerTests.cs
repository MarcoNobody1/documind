using DocuMind.Application.Abstractions;
using DocuMind.Application.UseCases;
using DocuMind.Domain.Entities;
using Microsoft.Extensions.AI;

namespace DocuMind.UnitTests.Application;

public class UploadDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_EmptyTextPdf_ReturnsWarningAndPersistsNoChunks()
    {
        var extractor = new FakeTextExtractor(
        [
            new PageText(1, string.Empty),
            new PageText(2, "   ")
        ]);
        var chunker = new FakeChunker();
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var repository = new FakeChunkRepository();

        var handler = new UploadDocumentHandler(extractor, chunker, embeddingGenerator, repository);

        var result = await handler.HandleAsync("scanned.pdf", Stream.Null);

        Assert.NotNull(result.Warning);
        Assert.Equal(0, result.ChunkCount);
        Assert.Equal(2, result.PageCount);
        Assert.NotNull(repository.LastDocument);
        Assert.Empty(repository.LastChunks!);
        Assert.False(chunker.WasCalled, "Chunker should not run when there is no extractable text.");
        Assert.False(embeddingGenerator.WasCalled, "Embedding generation should not run when there is no extractable text.");
    }

    [Fact]
    public async Task HandleAsync_TextPdf_ChunksEmbedsAndPersistsWithNoWarning()
    {
        var extractor = new FakeTextExtractor(
        [
            new PageText(1, "Some real extracted text.")
        ]);
        var chunker = new FakeChunker();
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var repository = new FakeChunkRepository();

        var handler = new UploadDocumentHandler(extractor, chunker, embeddingGenerator, repository);

        var result = await handler.HandleAsync("handbook.pdf", Stream.Null);

        Assert.Null(result.Warning);
        Assert.Equal(chunker.ChunksToReturn.Count, result.ChunkCount);
        Assert.NotNull(repository.LastChunks);
        Assert.Equal(chunker.ChunksToReturn.Count, repository.LastChunks!.Count);
        Assert.All(repository.LastChunks!, chunk => Assert.NotNull(chunk.Embedding));
    }

    [Fact]
    public async Task HandleAsync_EmbeddingCountDoesNotMatchChunkCount_Throws()
    {
        var extractor = new FakeTextExtractor(
        [
            new PageText(1, "Some real extracted text.")
        ]);
        var chunker = new FakeChunker(); // returns 2 chunks
        var embeddingGenerator = new FakeEmbeddingGenerator(forcedCount: 1); // only 1 embedding
        var repository = new FakeChunkRepository();

        var handler = new UploadDocumentHandler(extractor, chunker, embeddingGenerator, repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync("mismatch.pdf", Stream.Null));

        Assert.Contains("does not match chunk count", ex.Message);
        Assert.Null(repository.LastDocument);
    }

    private sealed class FakeTextExtractor(IReadOnlyList<PageText> pages) : ITextExtractor
    {
        public Task<IReadOnlyList<PageText>> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
            => Task.FromResult(pages);
    }

    private sealed class FakeChunker : IChunker
    {
        public bool WasCalled { get; private set; }

        public List<TextChunk> ChunksToReturn { get; } =
        [
            new TextChunk(1, 0, "chunk one"),
            new TextChunk(1, 1, "chunk two")
        ];

        public IReadOnlyList<TextChunk> Chunk(IReadOnlyList<PageText> pages)
        {
            WasCalled = true;
            return ChunksToReturn;
        }
    }

    private sealed class FakeEmbeddingGenerator(int? forcedCount = null) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public bool WasCalled { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            var count = forcedCount ?? values.Count();
            var embeddings = new GeneratedEmbeddings<Embedding<float>>(
                Enumerable.Range(0, count)
                    .Select(_ => new Embedding<float>(new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]))));
            return Task.FromResult(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeChunkRepository : IChunkRepository
    {
        public Document? LastDocument { get; private set; }

        public IReadOnlyList<DocumentChunk>? LastChunks { get; private set; }

        public Task AddDocumentAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        {
            LastDocument = document;
            LastChunks = chunks;
            return Task.CompletedTask;
        }
    }
}
