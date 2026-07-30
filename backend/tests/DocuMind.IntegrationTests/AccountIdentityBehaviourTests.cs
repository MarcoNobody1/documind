using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.IntegrationTests;

/// <summary>
/// Exercises the three account behaviours the API delegates entirely to ASP.NET Core Identity:
/// duplicate-email rejection, wrong-password rejection, and lockout after repeated failures.
/// </summary>
/// <remarks>
/// <para>
/// Why these needed a test at all. Each of the three is configuration rather than code — the
/// endpoints call <c>UserManager.CreateAsync</c> and
/// <c>SignInManager.PasswordSignInAsync(..., lockoutOnFailure: true)</c> and let Identity decide the
/// outcome. Code review can confirm the call is spelled correctly; it cannot confirm the framework
/// then does what the spec promises. <c>sdd-verify</c> of <c>phase2-auth</c> flagged all three as
/// having no verification evidence of any kind, automated or manual, and singled out lockout as the
/// one requirement with nothing behind it whatsoever.
/// </para>
/// <para>
/// Lockout is the sharpest case. The only thing standing between the login endpoint and an
/// unlimited online password-guessing attempt is that <c>lockoutOnFailure</c> argument, which
/// defaults to <c>false</c> — a default that fails silently, since a login endpoint with lockout
/// disabled behaves identically to one with it enabled right up until someone attacks it. So this
/// asserts the threshold empirically and, more importantly, asserts that the <em>correct</em>
/// password is refused once the account is locked. Counting failures is bookkeeping; refusing the
/// right password is the protection.
/// </para>
/// <para>
/// These construct Identity against a real Postgres rather than through HTTP. The endpoints' own
/// HTTP shape (status codes, which cookies are attached to a failed login) is separately pinned by
/// <c>EndpointSecurityMetadataTests</c> and by recorded runtime evidence; what is untested and
/// framework-owned is the behaviour underneath, so that is what these reach for. Identity
/// registration below deliberately mirrors <c>Program.cs</c> exactly, including the absence of any
/// <c>IdentityOptions.Lockout</c> override — if production ever overrides the threshold, this test
/// must be updated deliberately rather than keep passing against a stale assumption. That detection
/// was confirmed, not assumed: forcing <c>MaxFailedAccessAttempts</c> to 99 was verified to fail the
/// lockout assertion before this file was committed.
/// </para>
/// <para>
/// <strong>Known limit.</strong> These tests supply <c>lockoutOnFailure: true</c> themselves, so
/// they verify Identity's behaviour and threshold but cannot catch that argument being flipped to
/// <c>false</c> at the endpoint's own call site. Only a test driving the real HTTP endpoint closes
/// that, tracked as a Known follow-up in both READMEs.
/// </para>
/// </remarks>
[Collection(PostgresCollection.Name)]
public class AccountIdentityBehaviourTests
{
    /// <summary>
    /// Identity's documented default (<c>IdentityOptions.Lockout.MaxFailedAccessAttempts</c>).
    /// Asserted rather than trusted: the spec's requirement is "locks at the framework default", so
    /// the number itself is part of what needs verifying.
    /// </summary>
    private const int ExpectedFrameworkDefaultMaxFailedAttempts = 5;

    private const string ValidPassword = "Verify1!pass";
    private const string WrongPassword = "Wrong1!pass";

    private readonly PostgresFixture _fixture;

    public AccountIdentityBehaviourTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegistrationRejectsASecondAccountWithTheSameEmail()
    {
        await using var services = BuildIdentityServices();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = UniqueEmail();

        var first = await userManager.CreateAsync(NewUser(email), ValidPassword);
        var second = await userManager.CreateAsync(NewUser(email), ValidPassword);

        Assert.True(first.Succeeded, Describe(first));
        Assert.False(second.Succeeded, "A second account with the same email must be rejected.");

        // Assert on the error code, not the message: messages are localizable and would make this
        // test fail for a language change rather than a behaviour change.
        Assert.Contains(
            second.Errors,
            error => error.Code is "DuplicateUserName" or "DuplicateEmail");
    }

    [Fact]
    public async Task LoginRejectsAWrongPasswordWithoutLockingTheAccount()
    {
        await using var services = BuildIdentityServices();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = services.GetRequiredService<SignInManager<ApplicationUser>>();
        var email = UniqueEmail();
        Assert.True((await userManager.CreateAsync(NewUser(email), ValidPassword)).Succeeded);

        var result = await signInManager.PasswordSignInAsync(
            email, WrongPassword, isPersistent: false, lockoutOnFailure: true);

        Assert.False(result.Succeeded);

        // Distinguishing these two matters: a single wrong attempt reporting LockedOut would mean
        // the threshold is misconfigured to 1, which is a denial-of-service against the real user
        // rather than protection.
        Assert.False(result.IsLockedOut, "One wrong password must not lock the account.");
    }

    [Fact]
    public async Task RepeatedFailedLoginsLockTheAccountAndThenRefuseTheCorrectPassword()
    {
        await using var services = BuildIdentityServices();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = services.GetRequiredService<SignInManager<ApplicationUser>>();
        var email = UniqueEmail();
        Assert.True((await userManager.CreateAsync(NewUser(email), ValidPassword)).Succeeded);

        // Deliberately loops past the expected threshold instead of asserting attempt-by-attempt:
        // the requirement is "locks at the framework default", so the attempt number at which
        // lockout first appears is the measurement, not an assumption baked into the loop bound.
        var attemptsUntilLockout = 0;
        for (var attempt = 1; attempt <= ExpectedFrameworkDefaultMaxFailedAttempts + 2; attempt++)
        {
            var result = await signInManager.PasswordSignInAsync(
                email, WrongPassword, isPersistent: false, lockoutOnFailure: true);

            Assert.False(result.Succeeded, "A wrong password must never succeed.");

            if (result.IsLockedOut)
            {
                attemptsUntilLockout = attempt;
                break;
            }
        }

        Assert.Equal(ExpectedFrameworkDefaultMaxFailedAttempts, attemptsUntilLockout);

        // The assertion that actually matters. Everything above only proves Identity counts; this
        // proves the count has teeth.
        //
        // Be precise about what it does NOT cover: this test passes lockoutOnFailure: true itself,
        // so it cannot catch someone flipping that argument to false in AccountEndpoints. It proves
        // the mechanism and the threshold, not the call site. Closing that last gap needs a test
        // that drives the real HTTP endpoint — recorded as a Known follow-up in both READMEs rather
        // than left as an unstated limitation of this file.
        var whileLockedOut = await signInManager.PasswordSignInAsync(
            email, ValidPassword, isPersistent: false, lockoutOnFailure: true);

        Assert.False(
            whileLockedOut.Succeeded,
            "The correct password must be refused while the account is locked out — otherwise the "
                + "lockout counter is bookkeeping with no effect.");
        Assert.True(whileLockedOut.IsLockedOut);
    }

    private static ApplicationUser NewUser(string email) =>
        new() { Id = Guid.NewGuid(), UserName = email, Email = email };

    /// <summary>
    /// A fresh address per test. The Postgres container is shared across the assembly, so a fixed
    /// address would make these tests interfere with each other and with any rerun.
    /// </summary>
    private static string UniqueEmail() => $"identity-{Guid.NewGuid():N}@example.com";

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    /// <summary>
    /// Mirrors <c>Program.cs</c>'s Identity registration: <c>AddIdentityCore</c> +
    /// EF stores + <c>AddSignInManager</c>, cookie authentication under
    /// <see cref="IdentityConstants.ApplicationScheme"/>, and — critically — no
    /// <c>IdentityOptions.Lockout</c> configuration, so the thresholds under test are the framework
    /// defaults production actually runs with.
    /// </summary>
    private ServiceProvider BuildIdentityServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<DocuMindDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString, npgsql => npgsql.UseVector()));

        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<DocuMindDbContext>()
            .AddSignInManager();

        // SignInManager resolves IAuthenticationSchemeProvider, so authentication has to be
        // registered even though no request is ever signed in here — every path under test is a
        // failure path, which returns before SignInAsync is reached.
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);

        services.AddHttpContextAccessor();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = provider };

        return provider;
    }
}
