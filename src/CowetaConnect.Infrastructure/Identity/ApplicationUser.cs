// src/CowetaConnect.Infrastructure/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace CowetaConnect.Infrastructure.Identity;

/// <summary>
/// Extends IdentityUser&lt;Guid&gt; with domain-specific properties.
/// UserName is always set equal to Email — we don't use a separate display name for login.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>Simple string role: Member | Owner | Admin.</summary>
    public string Role { get; set; } = "Member";

    /// <summary>Tracked separately from Identity's EmailConfirmed for domain clarity.</summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>Google OAuth subject claim — null for password-only accounts.</summary>
    public string? GoogleSubject { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
}
