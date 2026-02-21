// src/CowetaConnect.Application/Auth/Models/TokenResponse.cs
namespace CowetaConnect.Application.Auth.Models;

public record TokenResponse(
    string AccessToken,
    string TokenType = "Bearer");
