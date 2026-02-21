// src/CowetaConnect.Application/Auth/Validators/LoginDtoValidator.cs
using CowetaConnect.Application.Auth.Dtos;
using FluentValidation;

namespace CowetaConnect.Application.Auth.Validators;

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
