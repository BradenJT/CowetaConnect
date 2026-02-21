// src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContext.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CowetaConnect.Infrastructure.Data;

public class CowetaConnectDbContext(DbContextOptions<CowetaConnectDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BusinessTag> BusinessTags => Set<BusinessTag>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<BusinessPhoto> BusinessPhotos => Set<BusinessPhoto>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CowetaConnectDbContext).Assembly);
    }
}
