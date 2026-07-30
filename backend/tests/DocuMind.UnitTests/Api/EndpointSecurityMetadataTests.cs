using DocuMind.Api.Endpoints;
using DocuMind.Application.Abstractions;
using DocuMind.Application.UseCases;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.UnitTests.Api;

/// <summary>
/// Asserts the security posture the route table *declares*, by reading the metadata ASP.NET Core
/// actually builds for each endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Why this exists as a test rather than a comment. Two of this project's security properties are
/// expressed as endpoint metadata rather than as code inside a handler, which makes them invisible
/// to every other test in the suite:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>POST /api/documents</c> requires antiforgery validation only because nothing calls
/// <c>DisableAntiforgery()</c> on it. It is protected by an <em>absence</em>, and an absence is
/// exactly what a reviewer's eye skips and what no unit test on the handler can notice. Re-adding
/// that one call would compile, pass every existing test, and silently remove CSRF protection from
/// the only state-changing multipart endpoint in the app.
/// </description></item>
/// <item><description>
/// Every endpoint requires authorization. The owner-isolation integration test proves the
/// <em>repository</em> filters by owner; it says nothing about whether the route in front of it
/// still demands a principal. Deleting a <c>RequireAuthorization()</c> call would leave that test
/// green while opening the endpoint to anonymous callers — at which point
/// <c>ClaimsPrincipal.GetOwnerId()</c> throws and the failure mode is a 500, not a 401.
/// </description></item>
/// </list>
/// <para>
/// What this deliberately does not test: that the antiforgery middleware correctly rejects a
/// request carrying a bad token. That is framework behaviour, covered by ASP.NET Core's own suite,
/// and asserting it here would require booting the full app (and therefore Postgres and Azure
/// OpenAI) to re-verify someone else's code. What belongs to this repository is the declaration,
/// and the declaration is what these tests pin.
/// </para>
/// </remarks>
public class EndpointSecurityMetadataTests
{
    [Fact]
    public void UploadEndpointRequiresAntiforgeryValidation()
    {
        var endpoint = FindEndpoint("/api/documents", HttpMethods.Post);

        var antiforgery = endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>();

        // Present AND requiring validation. Asserting only "not null" would pass even after a
        // DisableAntiforgery() call, because that call does not remove the metadata — it adds an
        // entry whose RequiresValidation is false.
        Assert.NotNull(antiforgery);
        Assert.True(
            antiforgery.RequiresValidation,
            "POST /api/documents must require antiforgery validation. A DisableAntiforgery() call "
                + "was almost certainly re-added: see ADR-C in README.md before changing this.");
    }

    [Fact]
    public void ChatEndpointRequiresNoAntiforgeryValidation()
    {
        var endpoint = FindEndpoint("/api/chat", HttpMethods.Post);

        var antiforgery = endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>();

        // The asymmetry recorded in ADR-C, pinned as an executable claim rather than left as prose:
        // this endpoint takes JSON, and a cross-origin HTML form cannot send application/json, so
        // it cannot be forged the way a multipart POST can. If this ever starts requiring a token,
        // it means the endpoint's shape changed (form binding? a filter added app-wide?) and the
        // ADR-C reasoning needs re-deriving rather than the assertion needs relaxing.
        Assert.True(
            antiforgery is null || !antiforgery.RequiresValidation,
            "POST /api/chat is not expected to require antiforgery validation (ADR-C). If the "
                + "endpoint now binds a form, revisit the decision instead of this assertion.");
    }

    [Fact]
    public void ListEndpointRequiresNoAntiforgeryValidation()
    {
        var endpoint = FindEndpoint("/api/documents", HttpMethods.Get);

        var antiforgery = endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>();

        // GET is a safe method by definition; requiring a token here would be friction with no
        // corresponding risk.
        Assert.True(antiforgery is null || !antiforgery.RequiresValidation);
    }

    [Theory]
    [InlineData("/api/documents", "POST")]
    [InlineData("/api/documents", "GET")]
    [InlineData("/api/chat", "POST")]
    public void DocumentAndChatEndpointsRequireAuthorization(string pattern, string method)
    {
        var endpoint = FindEndpoint(pattern, method);

        Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
    }

    /// <summary>
    /// Builds the real route table by invoking the same <c>Map*Endpoints</c> extension methods
    /// <c>Program.cs</c> does, then resolves one endpoint by route pattern and HTTP method.
    /// </summary>
    /// <remarks>
    /// A host is built (rather than a hand-rolled <see cref="IEndpointRouteBuilder"/>) so the
    /// metadata under assertion is produced by the same framework code path production uses —
    /// including the automatic antiforgery metadata that <c>IFormFile</c> binding triggers, which
    /// is the entire subject of these tests. Nothing is started: no Kestrel, no database, no
    /// outbound client. Mapping an endpoint never resolves its handler's dependencies.
    /// </remarks>
    private static Endpoint FindEndpoint(string pattern, string method)
    {
        var builder = WebApplication.CreateSlimBuilder();

        // Minimal APIs decide whether a complex handler parameter is a DI service or the request
        // body by asking IServiceProviderIsService. In an empty container the handlers below are
        // inferred as JSON body parameters, which collides with the upload endpoint's
        // [FromForm] IFormFile and makes metadata inference throw "An action cannot use both form
        // and JSON body parameters" — before a single assertion runs.
        //
        // So they are registered, but with a factory that throws: these tests read metadata and
        // must never execute a handler. If a new endpoint introduces a new handler type, this list
        // goes stale and the tests fail with that same explicit framework error rather than
        // quietly asserting the wrong thing.
        RegisterForInferenceOnly<UploadDocumentHandler>(builder.Services);
        RegisterForInferenceOnly<AskQuestionHandler>(builder.Services);
        RegisterForInferenceOnly<IChunkRepository>(builder.Services);

        var app = builder.Build();

        app.MapDocumentsEndpoints();
        app.MapChatEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

        var match = endpoints
            .OfType<RouteEndpoint>()
            .SingleOrDefault(endpoint =>
                endpoint.RoutePattern.RawText == pattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        // A null here means the route was renamed or removed, not that the security posture is
        // fine. Fail loudly rather than let a NullReferenceException imply a framework problem.
        Assert.NotNull(match);

        return match;
    }

    /// <summary>
    /// Makes <typeparamref name="T"/> visible to minimal-API parameter-source inference without
    /// making it resolvable. Resolution is a bug in the test, not a scenario, so it throws.
    /// </summary>
    private static void RegisterForInferenceOnly<T>(IServiceCollection services)
        where T : class =>
        services.AddScoped<T>(_ => throw new InvalidOperationException(
            $"{typeof(T).Name} is registered so minimal APIs infer it as a DI parameter rather "
                + "than a request body. These tests assert endpoint metadata and never execute a "
                + "handler, so resolving it means the test is doing something it should not."));
}
