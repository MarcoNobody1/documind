using System.Runtime.CompilerServices;
using DocuMind.Application.Abstractions;
using DocuMind.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace DocuMind.Application.UseCases;

/// <summary>
/// Orchestrates the RAG chat pipeline: embed the question, retrieve the top-k most relevant
/// chunks owned by the caller, build a grounded prompt, and stream the answer. Citations are
/// resolved eagerly from the retrieved chunks' stored metadata — never parsed or inferred from the
/// model's free-text output — while answer tokens are streamed lazily.
/// </summary>
public class AskQuestionHandler
{
    private const string SystemPrompt =
        "You are DocuMind, an assistant that answers questions using ONLY the provided context "
        + "extracted from the user's uploaded documents. If the context does not contain the "
        + "answer, say so plainly instead of guessing. Do not fabricate page numbers or document "
        + "names; citations are handled separately from your answer.";

    private readonly IChunkRepository _chunkRepository;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatClient _chatClient;
    private readonly int _topK;

    public AskQuestionHandler(
        IChunkRepository chunkRepository,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IChatClient chatClient,
        int topK)
    {
        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "Retrieval top-k must be positive.");
        }

        _chunkRepository = chunkRepository;
        _embeddingGenerator = embeddingGenerator;
        _chatClient = chatClient;
        _topK = topK;
    }

    public async Task<AskAnswer> HandleAsync(Guid ownerId, string question, CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _embeddingGenerator.GenerateVectorAsync(question, cancellationToken: cancellationToken);
        var retrievedChunks = await _chunkRepository.SearchAsync(ownerId, queryEmbedding, _topK, cancellationToken);

        var citations = retrievedChunks
            .Select(chunk => new Citation(chunk.DocumentName, chunk.PageNumber))
            .Distinct()
            .ToList();

        var messages = BuildPrompt(question, retrievedChunks);

        return new AskAnswer(citations, StreamAnswerAsync(messages, cancellationToken));
    }

    private static List<ChatMessage> BuildPrompt(string question, IReadOnlyList<RetrievedChunk> retrievedChunks)
    {
        var context = retrievedChunks.Count == 0
            ? "(No relevant content was found in the uploaded documents.)"
            : string.Join(
                "\n\n---\n\n",
                retrievedChunks.Select(chunk => $"[Source: {chunk.DocumentName}, page {chunk.PageNumber}]\n{chunk.Content}"));

        return
        [
            new ChatMessage(ChatRole.System, SystemPrompt),
            new ChatMessage(ChatRole.User, $"Context:\n{context}\n\nQuestion: {question}")
        ];
    }

    private async IAsyncEnumerable<string> StreamAnswerAsync(
        List<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // `ChatOptions.Temperature` is intentionally left unset (null): gpt-5-mini (and the
        // gpt-5 series generally) may reject sampling parameters that gpt-4o accepted. Leaving
        // it null means the request omits the parameter entirely instead of sending a value the
        // deployment might refuse.
        await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}

/// <summary>
/// The outcome of a chat question: citations resolved eagerly from retrieval metadata, and the
/// answer streamed lazily as it is generated.
/// </summary>
/// <param name="Citations">The documents and pages the answer draws from, deduplicated.</param>
/// <param name="Tokens">The answer text, streamed as it is produced.</param>
public record AskAnswer(IReadOnlyList<Citation> Citations, IAsyncEnumerable<string> Tokens);
