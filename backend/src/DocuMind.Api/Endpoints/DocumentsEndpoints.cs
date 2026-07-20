using DocuMind.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoints for document upload and ingestion (Slice A). No authentication is required per the
/// Phase 1 MVP boundary — all documents live in a single shared store.
/// </summary>
public static class DocumentsEndpoints
{
    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents", UploadDocumentAsync)
            .WithName("UploadDocument");

        return app;
    }

    private static async Task<IResult> UploadDocumentAsync(
        [FromForm] IFormFile file,
        UploadDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Results.BadRequest(new { error = "The uploaded file is empty." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Only PDF files are supported." });
        }

        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(file.FileName, stream, cancellationToken);

        return Results.Ok(new UploadDocumentResponse(
            result.DocumentId,
            result.PageCount,
            result.ChunkCount,
            result.Warning));
    }
}

/// <summary>
/// API response for a completed (or partially completed, if <see cref="Warning"/> is present) upload.
/// </summary>
public record UploadDocumentResponse(Guid DocumentId, int PageCount, int ChunkCount, string? Warning);
