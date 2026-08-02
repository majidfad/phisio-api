using FluentValidation;

namespace Phisio.Application.DoctorPatients.Validators;

public class AssignPatientExercisesRequestValidator : AbstractValidator<AssignPatientExercisesRequest>
{
    public AssignPatientExercisesRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one exercise is required.");

        RuleFor(x => x.ScheduledDates)
            .NotEmpty()
            .WithMessage("At least one scheduled date is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ExerciseId).NotEmpty();
            item.RuleFor(i => i.Sets).InclusiveBetween(1, 50).When(i => i.Sets.HasValue);
            item.RuleFor(i => i.Reps).MaximumLength(50);
            item.RuleFor(i => i.ClinicianNote).MaximumLength(1000);
            item.RuleFor(i => i.PatientCue).MaximumLength(500);
        });
    }
}
