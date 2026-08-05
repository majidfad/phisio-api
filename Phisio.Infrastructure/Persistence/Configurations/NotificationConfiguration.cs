using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.NotificationId);

        builder.Property(n => n.NotificationId)
            .ValueGeneratedNever();

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Body)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.Data)
            .HasMaxLength(4000);

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.Type, n.CreatedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
