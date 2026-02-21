// src/CowetaConnect.Tests/Auth/RegisterDtoValidatorTests.cs
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Validators;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class RegisterDtoValidatorTests
{
    private readonly RegisterDtoValidator _validator = new();

    [TestMethod]
    public void ValidDto_PassesValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password1", "Jane Doe");
        var result = _validator.Validate(dto);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void InvalidEmail_FailsValidation()
    {
        var dto = new RegisterDto("not-an-email", "Password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Email"));
    }

    [TestMethod]
    public void PasswordTooShort_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Pass1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void PasswordNoUppercase_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void PasswordNoDigit_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void DisplayNameTooShort_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password1", "J");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "DisplayName"));
    }

    [TestMethod]
    public void EmptyEmail_FailsValidation()
    {
        var dto = new RegisterDto("", "Password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
    }
}
