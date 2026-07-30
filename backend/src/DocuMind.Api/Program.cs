using DocuMind.Api.Endpoints;
using DocuMind.Application;
using DocuMind.Infrastructure;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Retrieval:TopK is a tuning knob, not a secret, so it ships as a checked-in default in
// appsettings.json (see the Retrieval section) rather than through user-secrets. The Application
// layer takes it as a plain int so it stays unaware of the configuration source; only this
// composition root reads it.
var retrievalTopK = builder.Configuration.GetValue("Retrieval:TopK", DocuMind.Application.DependencyInjection.DefaultRetrievalTopK);
builder.Services.AddApplication(retrievalTopK);

builder.Services.AddInfrastructure(builder.Configuration);

// AddIdentityCore alone does not register SignInManager — an explicit .AddSignInManager() is
// required, or the first login request fails in DI with no compile-time signal. No role store is
// added because AddEntityFrameworkStores<TContext>() detects that DocuMindDbContext is an
// IdentityUserContext<ApplicationUser, Guid> (no AspNetRoles table), matching PR1's schema.
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<DocuMindDbContext>()
    .AddSignInManager();

// Registered explicitly (AddIdentityCore alone adds no authentication) rather than via the full
// AddIdentity, so the scheme can be named IdentityConstants.ApplicationScheme — the scheme
// SignInManager.SignInAsync signs into — and set as the default authenticate/challenge scheme.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "DocuMind.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Dev is plain http on :5092; browser willingness to accept a Secure cookie from
        // http://localhost varies by vendor, so the dev loop must not depend on that quirk.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // Cookie auth 302-redirects to an HTML login path by default. An API must never do that
        // to a fetch caller — the client would follow the redirect and receive HTML instead of a
        // 401, which would make the client's 401 handling unreachable. Precondition, not polish.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    // Must match the header name the Angular client sends (app.config.ts's
    // withXsrfConfiguration, added in a later PR), so neither side can silently drift.
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "DocuMind.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

const string AngularClientCorsPolicy = "AngularClient";
builder.Services.AddCors(options =>
{
    // Cookie authentication means the auth cookie must ride along with the Angular dev server's
    // cross-origin requests, which requires AllowCredentials() — only legal here because the
    // origin list is an explicit non-wildcard value, not "*".
    options.AddPolicy(AngularClientCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Run once, after Build() and before Run(): placing it in AddInfrastructure would break that
// method's documented invariant that DI registration never contacts the database, and placing it
// before Build() would let EF's design-time tooling (dotnet ef database update) execute it against
// a schema that may not exist yet — HostFactoryResolver intercepts at Build() and aborts before
// reaching here. Throws on failure (ADR-I): a retrieval path that is silently unscoped is worse
// than an API that refuses to start.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DocuMindDbContext>();
    await RetrievalPrerequisiteCheck.VerifyAsync(dbContext);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(AngularClientCorsPolicy);

// Order matters: authentication must run before authorization, and antiforgery validation needs
// the authenticated principal available (its tokens are identity-bound) — so UseAntiforgery runs
// after both, per ASP.NET Core's documented middleware ordering.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");

app.MapDocumentsEndpoints();
app.MapChatEndpoints();
app.MapAccountEndpoints();

app.Run();
