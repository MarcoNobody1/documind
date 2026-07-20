using DocuMind.Application.Exceptions;
using DocuMind.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoints for document upload and ingestion (Slice A). No authentication is required per the
/// Phase 1 MVP boundary — all documents live in a single shared store.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>Maximum accepted upload size (25 MB) for the Phase 1 MVP.</summary>
    private const long MaxUploadBytes = 25_000_000;

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents", UploadDocumentAsync)
            .WithName("UploadDocument")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes));

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

        if (file.Length > MaxUploadBytes)
        {
            return Results.BadRequest(new { error = $"The uploaded file exceeds the maximum allowed size of {MaxUploadBytes / 1_000_000} MB." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Only PDF files are supported." });
        }

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await handler.HandleAsync(file.FileName, stream, cancellationToken);

            return Results.Ok(new UploadDocumentResponse(
                result.DocumentId,
                result.PageCount,
                result.ChunkCount,
                result.Warning));
        }
        catch (InvalidDocumentException)
        {
            return Results.BadRequest(new { error = "The uploaded file is not a valid PDF." });
        }
    }
}

/// <summary>
/// API response for a completed (or partially completed, if <see cref="Warning"/> is present) upload.
/// </summary>
public record UploadDocumentResponse(Guid DocumentId, int PageCount, int ChunkCount, string? Warning);
