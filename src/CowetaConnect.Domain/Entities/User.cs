// src/CowetaConnect.Domain/Entities/User.cs
namespace CowetaConnect.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }           // Nullable — OAuth users have no password
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";        // Member | Owner | Admin
    public bool IsEmailVerified { get; set; }
    public string? GoogleSubject { get; set; }          // OAuth subject claim, nullable
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLogin { get; set; }

    public ICollection<Business> Businesses { get; set; } = [];
}
