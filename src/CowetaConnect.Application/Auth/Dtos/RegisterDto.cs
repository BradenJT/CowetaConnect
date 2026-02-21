// src/CowetaConnect.Application/Auth/Dtos/RegisterDto.cs
namespace CowetaConnect.Application.Auth.Dtos;

public record RegisterDto(string Email, string Password, string DisplayName);
