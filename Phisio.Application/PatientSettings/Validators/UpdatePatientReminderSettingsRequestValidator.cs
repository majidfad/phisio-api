using FluentValidation;
using Phisio.Domain.Enums;

namespace Phisio.Application.PatientSettings.Validators;

public sealed class UpdatePatientReminderSettingsRequestValidator
    : AbstractValidator<UpdatePatientReminderSettingsRequest>
{
    public UpdatePatientReminderSettingsRequestValidator()
    {
        RuleFor(x => x.PreferredReminderTime)
            .NotEmpty()
            .Must(BeValidTime)
            .WithMessage("Preferred reminder time must be a valid time (HH:mm or HH:mm:ss).");

        RuleFor(x => x.TimeZoneId)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId));

        RuleFor(x => x.RepeatMode)
            .IsInEnum();

        RuleFor(x => x.DaysOfWeekMask)
            .InclusiveBetween(0, ReminderSchedule.AllDaysMask);

        RuleFor(x => x.DaysOfWeekMask)
            .GreaterThan(0)
            .When(x => x.ExerciseRemindersEnabled && x.RepeatMode == ReminderRepeatMode.DaysOfWeek)
            .WithMessage("Select at least one weekday for reminders.");

        RuleFor(x => x.IntervalDays)
            .InclusiveBetween(1, 30)
            .When(x => x.RepeatMode == ReminderRepeatMode.Interval)
            .WithMessage("Interval must be between 1 and 30 days.");

        RuleFor(x => x.FollowUpReminderTime)
            .Must(value => string.IsNullOrWhiteSpace(value) || BeValidTime(value!))
            .WithMessage("Follow-up reminder time must be a valid time (HH:mm or HH:mm:ss).");

        RuleFor(x => x)
            .Must(HaveFollowUpAfterPrimary)
            .When(x => x.FollowUpEnabled)
            .WithMessage("Follow-up reminder time must be later than the primary reminder time.");
    }

    private static bool BeValidTime(string value) =>
        TimeOnly.TryParse(value, out _);

    private static bool HaveFollowUpAfterPrimary(UpdatePatientReminderSettingsRequest request)
    {
        if (!TimeOnly.TryParse(request.PreferredReminderTime, out var primary))
        {
            return true;
        }

        var followUpRaw = string.IsNullOrWhiteSpace(request.FollowUpReminderTime)
            ? "18:00"
            : request.FollowUpReminderTime;

        if (!TimeOnly.TryParse(followUpRaw, out var followUp))
        {
            return false;
        }

        return followUp > primary;
    }
}
