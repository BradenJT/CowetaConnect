// src/CowetaConnect.Infrastructure/Data/Configurations/BusinessTagConfiguration.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class BusinessTagConfiguration : IEntityTypeConfiguration<BusinessTag>
{
    public void Configure(EntityTypeBuilder<BusinessTag> builder)
    {
        builder.ToTable("business_tags");
        builder.HasKey(bt => new { bt.BusinessId, bt.TagId });

        builder.HasOne(bt => bt.Business)
            .WithMany(b => b.BusinessTags)
            .HasForeignKey(bt => bt.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bt => bt.Tag)
            .WithMany(t => t.BusinessTags)
            .HasForeignKey(bt => bt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
