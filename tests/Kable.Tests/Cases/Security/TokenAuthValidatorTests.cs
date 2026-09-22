namespace Kable.Tests.Cases.Security;

using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Xunit;

public sealed class TokenAuthValidatorTests
{
    [Fact]
    public void ValidateToken_WhenAuthDisabled_AlwaysReturnsTrue()
    {
        var validator = new TokenAuthValidator(new SecurityTransportOptions
        {
            RequireTokenAuthentication = false
        });

        Assert.True(validator.ValidateToken(null));
        Assert.True(validator.ValidateToken("any-random-token"));
        Assert.True(validator.ValidateHeader(null));
    }

    [Fact]
    public void ValidateToken_StaticTokens_ValidatesCorrectly()
    {
        var validator = new TokenAuthValidator(new SecurityTransportOptions
        {
            RequireTokenAuthentication = true,
            ValidTokens = { "secret-token-123", "secret-token-456" }
        });

        Assert.True(validator.ValidateToken("secret-token-123"));
        Assert.True(validator.ValidateToken("secret-token-456"));
        Assert.False(validator.ValidateToken("wrong-token"));
        Assert.False(validator.ValidateToken(null));
        Assert.False(validator.ValidateToken(""));
    }

    [Fact]
    public void ValidateHeader_BearerPrefix_StripsAndValidates()
    {
        var validator = new TokenAuthValidator(new SecurityTransportOptions
        {
            RequireTokenAuthentication = true,
            ValidTokens = { "valid-jwt-token" }
        });

        Assert.True(validator.ValidateHeader("Bearer valid-jwt-token"));
        Assert.True(validator.ValidateHeader("bearer valid-jwt-token"));
        Assert.True(validator.ValidateHeader("valid-jwt-token"));
        Assert.False(validator.ValidateHeader("Bearer invalid-token"));
        Assert.False(validator.ValidateHeader(null));
        Assert.False(validator.ValidateHeader("   "));
    }

    [Fact]
    public void CustomTokenValidator_InvokesCustomPredicate()
    {
        var validator = new TokenAuthValidator(new SecurityTransportOptions
        {
            RequireTokenAuthentication = true,
            CustomTokenValidator = token => token.StartsWith("CUSTOM_", System.StringComparison.Ordinal)
        });

        Assert.True(validator.ValidateToken("CUSTOM_VALID_TOKEN"));
        Assert.False(validator.ValidateToken("STANDARD_TOKEN"));
        Assert.True(validator.ValidateHeader("Bearer CUSTOM_VALID_TOKEN"));
    }
}
