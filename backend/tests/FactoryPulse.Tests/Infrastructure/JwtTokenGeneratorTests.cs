using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Tests.Infrastructure;

public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator CreateGenerator()
    {
        var settings = new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Key = "this-is-a-test-signing-key-of-32+chars!",
            AccessTokenMinutes = 30
        };

        return new JwtTokenGenerator(Options.Create(settings));
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeSubjectEmailAndRoleClaims()
    {
        var generator = CreateGenerator();
        var userId = Guid.NewGuid().ToString();

        var accessToken = generator.GenerateAccessToken(userId, "user@test.com", new[] { "Admin", "Manager" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Token);

        jwt.Subject.ShouldBe(userId);
        jwt.Claims.ShouldContain(claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == "user@test.com");

        var roles = jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value).ToList();
        roles.ShouldContain("Admin");
        roles.ShouldContain("Manager");
    }

    [Fact]
    public void GenerateAccessToken_ShouldSetIssuerAudienceAndExpiry()
    {
        var generator = CreateGenerator();

        var accessToken = generator.GenerateAccessToken(Guid.NewGuid().ToString(), "user@test.com", new[] { "Viewer" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Token);

        jwt.Issuer.ShouldBe("TestIssuer");
        jwt.Audiences.ShouldContain("TestAudience");
        accessToken.ExpiresAtUtc.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateAccessToken_ShouldProduceAUniqueJtiPerToken()
    {
        var generator = CreateGenerator();

        var first = generator.GenerateAccessToken(Guid.NewGuid().ToString(), "a@test.com", new[] { "Viewer" });
        var second = generator.GenerateAccessToken(Guid.NewGuid().ToString(), "b@test.com", new[] { "Viewer" });

        first.Token.ShouldNotBe(second.Token);
    }
}
