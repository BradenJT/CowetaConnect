// src/CowetaConnect.Tests/Auth/JwtTokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using CowetaConnect.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class JwtTokenServiceTests
{
    private JwtTokenService _sut = null!;
    private RsaSecurityKey _signingKey = null!;

    [TestInitialize]
    public void Setup()
    {
        var rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://test.local",
                ["Jwt:Audience"] = "https://client.local",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            })
            .Build();

        _sut = new JwtTokenService(_signingKey, config);
    }

    [TestMethod]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Member");

        Assert.IsNotNull(token);
        var handler = new JwtSecurityTokenHandler();
        Assert.IsTrue(handler.CanReadToken(token));
    }

    [TestMethod]
    public void GenerateAccessToken_HasCorrectClaims()
    {
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Owner");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.AreEqual("user-123", jwt.Subject);
        Assert.AreEqual("test@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.IsNotNull(jwt.Claims.FirstOrDefault(c => c.Type == "jti"));
    }

    [TestMethod]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var before = DateTime.UtcNow;
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Member");
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Allow 5 seconds of clock slack in tests.
        Assert.IsTrue(jwt.ValidTo >= before.AddMinutes(15).AddSeconds(-5));
        Assert.IsTrue(jwt.ValidTo <= after.AddMinutes(15).AddSeconds(5));
    }

    [TestMethod]
    public void GenerateRefreshToken_Returns86CharBase64()
    {
        var token = _sut.GenerateRefreshToken();

        Assert.IsNotNull(token);
        // 64 bytes in Base64 = 88 chars (with padding). Length >= 86.
        Assert.IsGreaterThanOrEqualTo(86, token.Length);
    }

    [TestMethod]
    public void GenerateRefreshToken_IsUnique()
    {
        var t1 = _sut.GenerateRefreshToken();
        var t2 = _sut.GenerateRefreshToken();

        Assert.AreNotEqual(t1, t2);
    }

    [TestMethod]
    public void HashToken_IsDeterministic()
    {
        var token = "some-raw-token";
        var hash1 = _sut.HashToken(token);
        var hash2 = _sut.HashToken(token);

        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void HashToken_DifferentInputsDifferentHashes()
    {
        var h1 = _sut.HashToken("token-a");
        var h2 = _sut.HashToken("token-b");

        Assert.AreNotEqual(h1, h2);
    }
}
