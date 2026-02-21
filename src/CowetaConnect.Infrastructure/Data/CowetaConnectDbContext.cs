// src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContext.cs
using CowetaConnect.Domain.Entities;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CowetaConnect.Infrastructure.Data;

public class CowetaConnectDbContext(DbContextOptions<CowetaConnectDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // Identity's base class exposes: Users, Roles, UserClaims, UserLogins, UserTokens, etc.
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BusinessTag> BusinessTags => Set<BusinessTag>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<BusinessPhoto> BusinessPhotos => Set<BusinessPhoto>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // MUST be called first — wires up Identity schema

        modelBuilder.HasPostgresExtension("postgis");

        // Rename Identity tables to match project snake_case conventions.
        modelBuilder.Entity<ApplicationUser>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CowetaConnectDbContext).Assembly);
    }
}
