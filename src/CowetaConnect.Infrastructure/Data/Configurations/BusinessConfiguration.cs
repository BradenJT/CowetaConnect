// src/CowetaConnect.Infrastructure/Data/Configurations/BusinessConfiguration.cs
using CowetaConnect.Domain.Entities;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Slug).HasMaxLength(220).IsRequired();
        builder.HasIndex(b => b.Slug).IsUnique();

        builder.Property(b => b.Description).IsRequired(false);
        builder.Property(b => b.Phone).HasMaxLength(20).IsRequired(false);
        builder.Property(b => b.Email).HasMaxLength(320).IsRequired(false);
        builder.Property(b => b.Website).IsRequired(false);
        builder.Property(b => b.AddressLine1).HasMaxLength(200).IsRequired(false);
        builder.Property(b => b.City).HasMaxLength(100).IsRequired();
        builder.Property(b => b.State).HasMaxLength(2).HasDefaultValue("OK");
        builder.Property(b => b.Zip).HasMaxLength(10).IsRequired(false);

        // PostGIS geography column — requires UseNetTopologySuite() on the DbContext options
        builder.Property(b => b.Location).HasColumnType("geography(Point,4326)");

        // JSONB for optional service area polygon
        builder.Property(b => b.ServiceAreaGeoJson).HasColumnType("jsonb").IsRequired(false);

        builder.Property(b => b.IsActive).HasDefaultValue(true);
        builder.Property(b => b.IsVerified).HasDefaultValue(false);

        // Indexes per DATA_MODEL.md
        builder.HasIndex(b => b.CategoryId);
        builder.HasIndex(b => b.City);
        builder.HasIndex(b => b.Location).HasMethod("GIST");

        // Shadow FK to ApplicationUser — no navigation property (ApplicationUser lives in Infrastructure).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Category)
            .WithMany(c => c.Businesses)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter: auto-exclude inactive listings from all queries.
        // Call .IgnoreQueryFilters() on a query to bypass when needed (e.g., admin views).
        builder.HasQueryFilter(b => b.IsActive);
    }
}
