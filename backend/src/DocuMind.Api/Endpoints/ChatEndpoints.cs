using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using DocuMind.Api.Extensions;
using DocuMind.Application.UseCases;
using DocuMind.Domain.ValueObjects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoint for retrieval-augmented chat. Requires authentication (Phase 2, PR4): retrieval is
/// scoped to the caller's own documents only (ADR-C: no antiforgery filter here — see the
/// deliberate CSRF asymmetry recorded in README.md; protection instead rests on the JSON
/// content-type forcing a CORS preflight, a non-wildcard CORS origin, and SameSite cookie scoping).
/// </summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", AskQuestionAsync)
            .WithName("AskQuestion")
            .RequireAuthorization();

        return app;
    }

    private static ServerSentEventsResult<string> AskQuestionAsync(
        ChatRequest request,
        AskQuestionHandler handler,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetOwnerId();
        return TypedResults.ServerSentEvents(StreamAsync(ownerId, request, handler, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> StreamAsync(
        Guid ownerId,
        ChatRequest request,
        AskQuestionHandler handler,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answer = await handler.HandleAsync(ownerId, request.Question, cancellationToken);

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
/// <param name="Question">The user's question, answered against the caller's own documents only.</param>
public record ChatRequest(string Question);
