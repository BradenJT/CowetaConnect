// src/CowetaConnect.Tests/Auth/LoginCommandHandlerTests.cs
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class LoginCommandHandlerTests
{
    private Mock<IAuthUserService> _authService = null!;
    private Mock<IJwtTokenService> _tokenService = null!;
    private Mock<IRefreshTokenRepository> _refreshRepo = null!;
    private LoginCommandHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService = new Mock<IAuthUserService>();
        _tokenService = new Mock<IJwtTokenService>();
        _refreshRepo = new Mock<IRefreshTokenRepository>();

        _sut = new LoginCommandHandler(
            _authService.Object,
            _tokenService.Object,
            _refreshRepo.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [TestMethod]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("127.0.0.1", default)).ReturnsAsync(0);
        _authService.Setup(s => s.ValidateCredentialsAsync("user@test.com", "Password1", default))
            .ReturnsAsync(new AuthUserResult(true, "user-1", "user@test.com", "Member"));
        _authService.Setup(s => s.ClearFailedLoginsAsync("127.0.0.1", default)).Returns(Task.CompletedTask);
        _authService.Setup(s => s.UpdateLastLoginAsync("user-1", default)).Returns(Task.CompletedTask);
        _tokenService.Setup(s => s.GenerateAccessToken("user-1", "user@test.com", "Member"))
            .Returns("access-token-jwt");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-refresh");
        _tokenService.Setup(s => s.HashToken("raw-refresh")).Returns("hashed-refresh");
        _refreshRepo.Setup(r => r.StoreAsync("user-1", "hashed-refresh", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, rawRefresh) = await _sut.Handle(
            new LoginCommand(new LoginDto("user@test.com", "Password1"), "127.0.0.1"),
            default);

        Assert.AreEqual("access-token-jwt", token.AccessToken);
        Assert.AreEqual("raw-refresh", rawRefresh);
    }

    [TestMethod]
    public async Task Handle_InvalidCredentials_RecordsFailedAttempt()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("127.0.0.1", default)).ReturnsAsync(0);
        _authService.Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new AuthUserResult(false, null, null, null, "Invalid credentials."));
        _authService.Setup(s => s.RecordFailedLoginAsync("127.0.0.1", default)).ReturnsAsync(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(new LoginCommand(new LoginDto("bad@test.com", "wrong"), "127.0.0.1"), default));

        _authService.Verify(s => s.RecordFailedLoginAsync("127.0.0.1", default), Times.Once);
    }

    [TestMethod]
    public async Task Handle_FiveFailedAttempts_ThrowsTooManyAttemptsException()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("10.0.0.1", default)).ReturnsAsync(5);

        await Assert.ThrowsAsync<TooManyAttemptsException>(() =>
            _sut.Handle(new LoginCommand(new LoginDto("u@t.com", "p"), "10.0.0.1"), default));

        // Credentials should NOT be checked when limit is exceeded.
        _authService.Verify(s => s.ValidateCredentialsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
