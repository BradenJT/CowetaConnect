// src/CowetaConnect.Infrastructure/Data/Configurations/BusinessHourConfiguration.cs
using CowetaConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Data.Configurations;

public class BusinessHourConfiguration : IEntityTypeConfiguration<BusinessHour>
{
    public void Configure(EntityTypeBuilder<BusinessHour> builder)
    {
        builder.ToTable("business_hours");
        builder.HasKey(bh => bh.Id);
        builder.Property(bh => bh.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(bh => bh.DayOfWeek).IsRequired();
        builder.Property(bh => bh.IsClosed).HasDefaultValue(false);

        builder.HasOne(bh => bh.Business)
            .WithMany(b => b.BusinessHours)
            .HasForeignKey(bh => bh.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
