using FluentValidation;

namespace Phisio.Application.DoctorPatients.Validators;

public class AssignPatientExercisesRequestValidator : AbstractValidator<AssignPatientExercisesRequest>
{
    public AssignPatientExercisesRequestValidator()
    {
        RuleFor(request => request.ExerciseIds)
            .NotEmpty();

        RuleFor(request => request.ScheduledDates)
            .NotEmpty();
    }
}
