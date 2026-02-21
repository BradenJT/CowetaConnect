// src/CowetaConnect.Infrastructure/Data/Configurations/BusinessPhotoConfiguration.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class BusinessPhotoConfiguration : IEntityTypeConfiguration<BusinessPhoto>
{
    public void Configure(EntityTypeBuilder<BusinessPhoto> builder)
    {
        builder.ToTable("business_photos");
        builder.HasKey(bp => bp.Id);
        builder.Property(bp => bp.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(bp => bp.BlobUrl).IsRequired();
        builder.Property(bp => bp.Caption).IsRequired(false);
        builder.Property(bp => bp.IsPrimary).HasDefaultValue(false);

        builder.HasOne(bp => bp.Business)
            .WithMany(b => b.BusinessPhotos)
            .HasForeignKey(bp => bp.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
