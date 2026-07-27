using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DocuMind.Application.UseCases;
using DocuMind.Domain.ValueObjects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoint for retrieval-augmented chat (Slice B). No authentication is required per the Phase 1
/// MVP boundary — questions are answered against the single shared document store.
/// </summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", AskQuestionAsync)
            .WithName("AskQuestion");

        return app;
    }

    private static ServerSentEventsResult<string> AskQuestionAsync(
        ChatRequest request,
        AskQuestionHandler handler,
        CancellationToken cancellationToken)
    {
        return TypedResults.ServerSentEvents(StreamAsync(request, handler, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> StreamAsync(
        ChatRequest request,
        AskQuestionHandler handler,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answer = await handler.HandleAsync(request.Question, cancellationToken);

        await foreach (var token in answer.Tokens.WithCancellation(cancellationToken))
        {
            yield return new SseItem<string>(token);
        }

        yield return new SseItem<string>(SerializeCitations(answer.Citations), "citations");
    }

    private static string SerializeCitations(IReadOnlyList<Citation> citations)
        // `JsonSerializerOptions.Web` matches ASP.NET Core's own default JSON conventions
        // (camelCase property names) — the same convention `Results.Ok(...)` uses elsewhere in
        // this API (e.g. `UploadDocumentResponse`). Calling `JsonSerializer.Serialize` with no
        // options here would default to PascalCase instead, silently mismatching every other
        // response shape and the Angular client's camelCase `Citation` model. Confirmed by an
        // actual `/api/chat` call: the unqualified call originally produced
        // `{"DocumentName":...}` while the client expects `documentName`.
        => JsonSerializer.Serialize(citations, JsonSerializerOptions.Web);
}

/// <summary>API request for a chat question. Single-turn by design — no conversation history.</summary>
/// <param name="Question">The user's question, answered against the shared document store.</param>
public record ChatRequest(string Question);
