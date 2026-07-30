using System.Security.Claims;

namespace DocuMind.Api.Extensions;

/// <summary>
/// Resolves the authenticated caller's owner id from the cookie principal, at the composition
/// root boundary — the Application layer never sees <see cref="ClaimsPrincipal"/> or
/// <see cref="HttpContext"/> (ADR-G).
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the caller's <see cref="Guid"/> owner id, parsed from <see cref="ClaimTypes.NameIdentifier"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the claim is absent or is not a parseable <see cref="Guid"/>. Deliberately never
    /// falls back to <see cref="Guid.Empty"/>: an empty owner id would run a syntactically valid,
    /// silently-scoped-to-nobody search and return zero rows with no error — a wrong answer
    /// disguised as an empty one, rather than a fault raised where the cause actually is. Every
    /// caller reaching this method is already required to be authenticated
    /// (<c>.RequireAuthorization()</c>), so this should be unreachable in practice; it exists as a
    /// defence against exactly that assumption being wrong.
    /// </exception>
    public static Guid GetOwnerId(this ClaimsPrincipal principal)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claimValue, out var ownerId))
        {
            throw new InvalidOperationException(
                "The authenticated principal has no parseable ClaimTypes.NameIdentifier claim. This "
                + "endpoint requires .RequireAuthorization(), so this indicates a misconfigured "
                + "authentication pipeline rather than a normal unauthenticated request.");
        }

        return ownerId;
    }
}
