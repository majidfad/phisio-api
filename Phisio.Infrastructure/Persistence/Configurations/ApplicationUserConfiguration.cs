using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion(
                role => role.ToString(),
                value => Enum.Parse<UserRole>(value))
            .HasMaxLength(20);

        builder.Property(u => u.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.ExerciseRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.PreferredReminderTime)
            .IsRequired()
            .HasColumnType("time")
            .HasDefaultValue(new TimeOnly(9, 0));

        builder.Property(u => u.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("Asia/Tehran");

        builder.Property(u => u.ReminderRepeatMode)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ReminderRepeatMode.Daily);

        builder.Property(u => u.ReminderDaysOfWeekMask)
            .IsRequired()
            .HasDefaultValue(0b1111111);

        builder.Property(u => u.ReminderIntervalDays)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(u => u.ReminderAnchorDate)
            .HasColumnType("date");

        builder.Property(u => u.ReminderFollowUpEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.ReminderFollowUpTime)
            .IsRequired()
            .HasColumnType("time")
            .HasDefaultValue(new TimeOnly(18, 0));

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique();

        builder.ConfigureCreatedAt(u => u.CreatedAt);
        builder.ConfigureSoftDelete(u => u.IsEnabled);
    }
}
