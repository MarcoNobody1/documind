using System.Security.Claims;
using DocuMind.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace DocuMind.Api.Endpoints;

/// <summary>
/// Endpoints for registration, login, logout and the current-user query (Phase 2). Built directly
/// on <see cref="UserManager{TUser}"/>/<see cref="SignInManager{TUser}"/> rather than
/// <c>MapIdentityApi</c>, which defaults to bearer tokens: this project's client is a browser SPA
/// (same-site in production, cross-port in dev), so cookie authentication — not bearer tokens — is
/// the correct transport.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register");

        group.MapPost("/login", LoginAsync)
            .WithName("Login");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .RequireAuthorization();

        // Anonymous by design: this is the client's bootstrap point, called regardless of auth
        // state (cookie auth carries no client-readable claims), so it must answer for both an
        // authenticated and an anonymous caller rather than redirect/reject at the route level.
        group.MapGet("/me", GetMeAsync)
            .WithName("Me")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            IssueAntiforgeryCookie(httpContext, antiforgery);
            return Results.ValidationProblem(ToErrorDictionary(result));
        }

        // Registration auto-signs-in in the same response — no separate login step (settled
        // decision, not an oversight).
        await signInManager.SignInAsync(user, isPersistent: false);

        IssueAntiforgeryCookie(httpContext, antiforgery);
        return Results.Ok(new AccountResponse(user.Id, user.Email!));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        // lockoutOnFailure MUST be passed explicitly as true — the framework default for this
        // argument does not increment the lockout counter, which would make the lockout
        // requirement aspirational rather than real.
        var result = await signInManager.PasswordSignInAsync(
            request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);

        // Identity changed (or a stale pre-login token is now being replaced) — reissue
        // unconditionally, on every branch, so a failed attempt does not leave the client with a
        // token bound to nothing.
        IssueAntiforgeryCookie(httpContext, antiforgery);

        if (result.IsLockedOut)
        {
            // Deliberate UX-over-enumeration-hardening choice for a portfolio demo: a locked
            // account tells the caller why, rather than folding into the generic message below.
            return Results.Problem(
                detail: "This account is temporarily locked. Try again later.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!result.Succeeded)
        {
            return Results.Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        // PasswordSignInAsync just succeeded against this exact email, so the user is guaranteed
        // to exist; the null-forgiving operators reflect that invariant, not an assumption.
        return Results.Ok(new AccountResponse(user!.Id, user.Email!));
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        await signInManager.SignOutAsync();
        IssueAntiforgeryCookie(httpContext, antiforgery);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        // Appended before the authenticated/anonymous branch below, so a 401 response carries a
        // fresh token too — the client calls this endpoint as its bootstrap point regardless of
        // auth state, so the token costs zero extra round trips and zero extra endpoints.
        IssueAntiforgeryCookie(httpContext, antiforgery);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        // Loaded through the store via the NameIdentifier claim rather than read directly off the
        // principal: Identity's default UserClaimsPrincipalFactory only emits NameIdentifier/Name/
        // security-stamp claims on the cookie principal, not Email, so reading ClaimTypes.Email
        // here would silently return null on every call.
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new AccountResponse(user.Id, user.Email!));
    }

    /// <summary>
    /// Issues a fresh, non-<c>HttpOnly</c> <c>XSRF-TOKEN</c> cookie bound to the current identity.
    /// Antiforgery tokens are identity-bound, so a token minted while anonymous fails validation
    /// after login — every account response must reissue one. JS-readability is the point, not a
    /// weakness: the request token alone is not a credential, and the paired HttpOnly cookie
    /// token (issued by the antiforgery middleware itself) is what makes double-submit sound.
    /// </summary>
    private static void IssueAntiforgeryCookie(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();

        httpContext.Response.Cookies.Append(
            "XSRF-TOKEN",
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                // Mirrors the CookieSecurePolicy.SameAsRequest/.Always split used for the other
                // two cookies (Program.cs) — CookieOptions has no policy enum, only a bool, so the
                // same "dev must not depend on browser Secure-cookie-over-http quirks" rule is
                // expressed as an explicit request-scheme check instead.
                Secure = !environment.IsDevelopment() || httpContext.Request.IsHttps,
                Path = "/",
            });
    }

    private static IDictionary<string, string[]> ToErrorDictionary(IdentityResult result)
        => result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}

/// <param name="Email">The account email, also used as the Identity user name.</param>
/// <param name="Password">The plaintext password, validated against Identity's default policy.</param>
public record RegisterRequest(string Email, string Password);

/// <param name="Email">The account email.</param>
/// <param name="Password">The plaintext password.</param>
public record LoginRequest(string Email, string Password);

/// <summary>The caller's identity, as returned by register/login/me.</summary>
public record AccountResponse(Guid Id, string Email);
