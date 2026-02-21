// src/CowetaConnect.Tests/Auth/GoogleSignInCommandHandlerTests.cs
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class GoogleSignInCommandHandlerTests
{
    private Mock<IAuthUserService> _authService = null!;
    private Mock<IJwtTokenService> _tokenService = null!;
    private Mock<IRefreshTokenRepository> _refreshRepo = null!;
    private GoogleSignInCommandHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService  = new Mock<IAuthUserService>();
        _tokenService = new Mock<IJwtTokenService>();
        _refreshRepo  = new Mock<IRefreshTokenRepository>();

        _sut = new GoogleSignInCommandHandler(
            _authService.Object,
            _tokenService.Object,
            _refreshRepo.Object,
            NullLogger<GoogleSignInCommandHandler>.Instance);
    }

    [TestMethod]
    public async Task Handle_NewUser_ReturnsTokenPair()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync("sub-new", "new@test.com", "New User", null, default))
            .ReturnsAsync(new AuthUserResult(true, "user-new", "new@test.com", "Member"));
        _authService
            .Setup(s => s.UpdateLastLoginAsync("user-new", default))
            .Returns(Task.CompletedTask);
        _tokenService
            .Setup(s => s.GenerateAccessToken("user-new", "new@test.com", "Member"))
            .Returns("jwt-new");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-refresh");
        _tokenService.Setup(s => s.HashToken("raw-refresh")).Returns("hashed");
        _refreshRepo
            .Setup(r => r.StoreAsync("user-new", "hashed", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, rawRefresh) = await _sut.Handle(
            new GoogleSignInCommand("sub-new", "new@test.com", "New User", null), default);

        Assert.AreEqual("jwt-new", token.AccessToken);
        Assert.AreEqual("raw-refresh", rawRefresh);
    }

    [TestMethod]
    public async Task Handle_ExistingGoogleUser_ReturnsTokensWithExistingRole()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync("sub-owner", "owner@test.com", "Owner User", null, default))
            .ReturnsAsync(new AuthUserResult(true, "user-owner", "owner@test.com", "Owner"));
        _authService
            .Setup(s => s.UpdateLastLoginAsync("user-owner", default))
            .Returns(Task.CompletedTask);
        _tokenService
            .Setup(s => s.GenerateAccessToken("user-owner", "owner@test.com", "Owner"))
            .Returns("jwt-owner");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-2");
        _tokenService.Setup(s => s.HashToken("raw-2")).Returns("hashed-2");
        _refreshRepo
            .Setup(r => r.StoreAsync("user-owner", "hashed-2", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, _) = await _sut.Handle(
            new GoogleSignInCommand("sub-owner", "owner@test.com", "Owner User", null), default);

        Assert.AreEqual("jwt-owner", token.AccessToken);
    }

    [TestMethod]
    public async Task Handle_UpsertFails_ThrowsInvalidOperationException()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new AuthUserResult(false, null, null, null, "DB write failed"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            _sut.Handle(
                new GoogleSignInCommand("sub-x", "fail@test.com", "Fail User", null), default));
    }
}
