using DocuMind.Application.Abstractions;
using DocuMind.Application.UseCases;
using Microsoft.Extensions.AI;

namespace DocuMind.UnitTests.Application;

public class AskQuestionHandlerTests
{
    [Fact]
    public async Task HandleAsync_PassesConfiguredTopKToRepository()
    {
        var repository = new FakeChunkRepository(
        [
            new RetrievedChunk(Guid.NewGuid(), "handbook.pdf", 3, "vacation policy text", 0.12)
        ]);
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var chatClient = new FakeChatClient(["The", " answer."]);

        var handler = new AskQuestionHandler(repository, embeddingGenerator, chatClient, topK: 5);

        await handler.HandleAsync("How many vacation days do I get?");

        Assert.Equal(5, repository.LastK);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCitationsFromRetrievedChunkMetadata_Deduplicated()
    {
        var repository = new FakeChunkRepository(
        [
            new RetrievedChunk(Guid.NewGuid(), "handbook.pdf", 3, "chunk one", 0.10),
            new RetrievedChunk(Guid.NewGuid(), "handbook.pdf", 3, "chunk two, same page", 0.11),
            new RetrievedChunk(Guid.NewGuid(), "policy-leave.pdf", 1, "parental leave text", 0.20)
        ]);
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var chatClient = new FakeChatClient(["answer"]);

        var handler = new AskQuestionHandler(repository, embeddingGenerator, chatClient, topK: 5);

        var answer = await handler.HandleAsync("cross-document question");

        Assert.Equal(2, answer.Citations.Count);
        Assert.Contains(answer.Citations, c => c.DocumentName == "handbook.pdf" && c.PageNumber == 3);
        Assert.Contains(answer.Citations, c => c.DocumentName == "policy-leave.pdf" && c.PageNumber == 1);
    }

    [Fact]
    public async Task HandleAsync_StreamsTokensFromChatClientInOrder()
    {
        var repository = new FakeChunkRepository([]);
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var chatClient = new FakeChatClient(["The", " answer", " streams."]);

        var handler = new AskQuestionHandler(repository, embeddingGenerator, chatClient, topK: 5);

        var answer = await handler.HandleAsync("question");

        var tokens = new List<string>();
        await foreach (var token in answer.Tokens)
        {
            tokens.Add(token);
        }

        Assert.Equal(["The", " answer", " streams."], tokens);
    }

    [Fact]
    public void Constructor_NonPositiveTopK_Throws()
    {
        var repository = new FakeChunkRepository([]);
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var chatClient = new FakeChatClient([]);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AskQuestionHandler(repository, embeddingGenerator, chatClient, topK: 0));
    }

    private sealed class FakeChunkRepository(IReadOnlyList<RetrievedChunk> chunksToReturn) : IChunkRepository
    {
        public int? LastK { get; private set; }

        public Task AddDocumentAsync(DocuMind.Domain.Entities.Document document, IReadOnlyList<DocuMind.Domain.Entities.DocumentChunk> chunks, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by AskQuestionHandler.");

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken = default)
        {
            LastK = k;
            return Task.FromResult(chunksToReturn);
        }
    }

    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]))));
            return Task.FromResult(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeChatClient(IReadOnlyList<string> tokensToYield) : IChatClient
    {
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var token in tokensToYield)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, token);
                await Task.Yield();
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("AskQuestionHandler only uses streaming responses.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
