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
/// principal. The upload endpoint additionally enforces antiforgery validation (Phase 2, PR5) —
/// see the comment at its map site for why that requires no code, and ADR-C in README.md for why
/// <c>/api/chat</c> deliberately does not.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>Maximum accepted upload size (25 MB) for the Phase 1 MVP.</summary>
    private const long MaxUploadBytes = 25_000_000;

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        // Antiforgery validation is ENFORCED here, and the *absence* of a .DisableAntiforgery()
        // call is the entire mechanism: minimal APIs attach antiforgery metadata automatically to
        // any endpoint that binds IFormFile, so this endpoint demands a valid token by default and
        // the only way to weaken it is to add a call back. Nothing here needs to opt in.
        //
        // That the sibling /api/chat endpoint gets no such filter is not an oversight but ADR-C's
        // deliberate asymmetry: a cross-origin HTML form can forge a multipart POST, and cannot
        // send application/json.
        //
        // This is asserted rather than asserted-in-prose. EndpointSecurityMetadataTests reads the
        // built endpoint's metadata and fails if antiforgery validation is ever switched off here
        // again, because a comment cannot fail a build.
        app.MapPost("/api/documents", UploadDocumentAsync)
            .WithName("UploadDocument")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .RequireAuthorization();

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
