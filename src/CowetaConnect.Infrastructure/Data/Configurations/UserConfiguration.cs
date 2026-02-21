// src/CowetaConnect.Infrastructure/Data/Configurations/UserConfiguration.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired(false);
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.AvatarUrl).IsRequired(false);
        builder.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("Member");
        builder.Property(u => u.IsEmailVerified).HasDefaultValue(false);
        builder.Property(u => u.GoogleSubject).IsRequired(false);
        builder.HasIndex(u => u.GoogleSubject);
    }
}
