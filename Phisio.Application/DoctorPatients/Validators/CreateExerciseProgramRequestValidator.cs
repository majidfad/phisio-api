using FluentValidation;
using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients.Validators;

public class CreateExerciseProgramRequestValidator : AbstractValidator<CreateExerciseProgramRequest>
{
    public CreateExerciseProgramRequestValidator()
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.EndDate)
            .Must((request, end) => end <= request.StartDate.AddYears(2))
            .WithMessage("Program duration must not exceed 2 years.");

        RuleFor(x => x.CadenceType).IsInEnum();

        RuleFor(x => x.DaysOfWeekMask)
            .InclusiveBetween(1, 127)
            .When(x => x.CadenceType == ExerciseProgramCadenceType.DaysOfWeek)
            .WithMessage("Select at least one day of the week.");

        RuleFor(x => x.IntervalDays)
            .InclusiveBetween(1, 30)
            .When(x => x.CadenceType == ExerciseProgramCadenceType.Interval)
            .WithMessage("Interval must be between 1 and 30 days.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Select at least one exercise.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ExerciseId).NotEmpty();
            item.RuleFor(i => i.Sets).InclusiveBetween(1, 50).When(i => i.Sets.HasValue);
            item.RuleFor(i => i.Reps).MaximumLength(50);
            item.RuleFor(i => i.HoldSeconds).InclusiveBetween(1, 600).When(i => i.HoldSeconds.HasValue);
            item.RuleFor(i => i.RestSeconds).InclusiveBetween(1, 600).When(i => i.RestSeconds.HasValue);
            item.RuleFor(i => i.Side).IsInEnum();
            item.RuleFor(i => i.ClinicianNote).MaximumLength(1000);
            item.RuleFor(i => i.PatientCue).MaximumLength(500);
        });
    }
}

public class UpdateExerciseProgramRequestValidator : AbstractValidator<UpdateExerciseProgramRequest>
{
    public UpdateExerciseProgramRequestValidator()
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.EndDate)
            .Must((request, end) => end <= request.StartDate.AddYears(2))
            .WithMessage("Program duration must not exceed 2 years.");

        RuleFor(x => x.CadenceType).IsInEnum();

        RuleFor(x => x.DaysOfWeekMask)
            .InclusiveBetween(1, 127)
            .When(x => x.CadenceType == ExerciseProgramCadenceType.DaysOfWeek)
            .WithMessage("Select at least one day of the week.");

        RuleFor(x => x.IntervalDays)
            .InclusiveBetween(1, 30)
            .When(x => x.CadenceType == ExerciseProgramCadenceType.Interval)
            .WithMessage("Interval must be between 1 and 30 days.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Select at least one exercise.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ExerciseId).NotEmpty();
            item.RuleFor(i => i.Sets).InclusiveBetween(1, 50).When(i => i.Sets.HasValue);
            item.RuleFor(i => i.Reps).MaximumLength(50);
            item.RuleFor(i => i.HoldSeconds).InclusiveBetween(1, 600).When(i => i.HoldSeconds.HasValue);
            item.RuleFor(i => i.RestSeconds).InclusiveBetween(1, 600).When(i => i.RestSeconds.HasValue);
            item.RuleFor(i => i.Side).IsInEnum();
            item.RuleFor(i => i.ClinicianNote).MaximumLength(1000);
            item.RuleFor(i => i.PatientCue).MaximumLength(500);
        });
    }
}
