// src/CowetaConnect.Infrastructure/Data/Configurations/SearchEventConfiguration.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class SearchEventConfiguration : IEntityTypeConfiguration<SearchEvent>
{
    public void Configure(EntityTypeBuilder<SearchEvent> builder)
    {
        builder.ToTable("search_events");
        builder.HasKey(se => se.Id);
        builder.Property(se => se.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(se => se.SessionHash).HasMaxLength(64).IsRequired(false);
        builder.Property(se => se.Keyword).HasMaxLength(500).IsRequired(false);
        builder.Property(se => se.CityFilter).HasMaxLength(100).IsRequired(false);
        builder.Property(se => se.UserCity).HasMaxLength(100).IsRequired(false);
        builder.Property(se => se.UserZip).HasMaxLength(10).IsRequired(false);

        // Native PostgreSQL array type — Npgsql maps Guid[] ↔ uuid[]
        builder.Property(se => se.ResultBusinessIds).HasColumnType("uuid[]");

        builder.Property(se => se.ClickedBusinessId).IsRequired(false);

        // BRIN index: cheap to maintain, good for append-only time-series data
        builder.HasIndex(se => se.OccurredAt).HasMethod("BRIN");
        builder.HasIndex(se => se.UserCity);

        // GIN index on tsvector(keyword) cannot be expressed via Fluent API.
        // Added via raw SQL in the migration (see Task 7).
    }
}
