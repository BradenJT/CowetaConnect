// src/CowetaConnect.Infrastructure/Identity/ApplicationUserConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Identity;

/// <summary>
/// Configures the custom columns added by ApplicationUser on top of IdentityUser&lt;Guid&gt;.
/// Identity's own columns (email, password_hash, security_stamp, etc.) are handled by Identity.
/// Table name override is done in CowetaConnectDbContext.OnModelCreating.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.AvatarUrl).IsRequired(false);
        builder.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("Member");
        builder.Property(u => u.IsEmailVerified).HasDefaultValue(false);
        builder.Property(u => u.GoogleSubject).IsRequired(false);
        builder.HasIndex(u => u.GoogleSubject);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.LastLogin).IsRequired(false);
    }
}
