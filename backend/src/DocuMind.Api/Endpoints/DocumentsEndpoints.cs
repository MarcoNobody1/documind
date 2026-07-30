using DocuMind.Api.Extensions;
using DocuMind.Application.Abstractions;
using DocuMind.Application.Exceptions;
using DocuMind.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoints for document upload, ingestion, and listing. Both require authentication (Phase 2,
/// PR4): every document belongs to exactly one owner, derived only from the authenticated
/// principal.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>Maximum accepted upload size (25 MB) for the Phase 1 MVP.</summary>
    private const long MaxUploadBytes = 25_000_000;

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents", UploadDocumentAsync)
            .WithName("UploadDocument")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .RequireAuthorization()
            // Minimal APIs attach anti-forgery metadata to any endpoint binding IFormFile, which
            // otherwise requires app.UseAntiforgery(). This endpoint is now authenticated
            // (RequireAuthorization above), so there IS an ambient session cookie a browser could
            // replay — the reasoning that justified disabling antiforgery no longer holds.
            // Removing this call is deliberately deferred to PR5, alongside the Angular-absolute-
            // URL fix (ADR-J) that removing it depends on; see ADR-C/ADR-D and the REVISIT marker
            // this comment replaces. Do not read this as the antiforgery decision being settled —
            // it is explicitly revisited in the very next PR.
            .DisableAntiforgery();

        app.MapGet("/api/documents", ListDocumentsAsync)
            .WithName("ListDocuments")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UploadDocumentAsync(
        [FromForm] IFormFile file,
        UploadDocumentHandler handler,
        ClaimsPrincipal principal,
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
            var ownerId = principal.GetOwnerId();
            var result = await handler.HandleAsync(ownerId, file.FileName, stream, cancellationToken);

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

    private static async Task<IResult> ListDocumentsAsync(
        IChunkRepository chunkRepository,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetOwnerId();
        var documents = await chunkRepository.ListDocumentsAsync(ownerId, cancellationToken);

        return Results.Ok(documents.Select(document => new DocumentListItem(
            document.Id,
            document.FileName,
            document.PageCount,
            document.UploadedAtUtc)));
    }
}

/// <summary>
/// API response item for <c>GET /api/documents</c>. Deliberately excludes any owner information —
/// the caller already knows it is asking about their own documents.
/// </summary>
public record DocumentListItem(Guid Id, string FileName, int PageCount, DateTime UploadedAtUtc);

/// <summary>
/// API response for a completed (or partially completed, if <see cref="Warning"/> is present) upload.
/// </summary>
public record UploadDocumentResponse(Guid DocumentId, int PageCount, int ChunkCount, string? Warning);
