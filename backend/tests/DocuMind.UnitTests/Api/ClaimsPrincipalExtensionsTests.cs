using System.Security.Claims;
using DocuMind.Api.Extensions;

namespace DocuMind.UnitTests.Api;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetOwnerId_ValidNameIdentifierClaim_ReturnsParsedGuid()
    {
        var ownerId = Guid.NewGuid();
        var principal = PrincipalWithNameIdentifier(ownerId.ToString());

        var result = principal.GetOwnerId();

        Assert.Equal(ownerId, result);
    }

    [Fact]
    public void GetOwnerId_MissingNameIdentifierClaim_Throws()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var ex = Assert.Throws<InvalidOperationException>(() => principal.GetOwnerId());

        Assert.Contains("NameIdentifier", ex.Message);
    }

    [Fact]
    public void GetOwnerId_UnparseableNameIdentifierClaim_Throws()
    {
        var principal = PrincipalWithNameIdentifier("not-a-guid");

        Assert.Throws<InvalidOperationException>(() => principal.GetOwnerId());
    }

    private static ClaimsPrincipal PrincipalWithNameIdentifier(string value)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, value)]);
        return new ClaimsPrincipal(identity);
    }
}
