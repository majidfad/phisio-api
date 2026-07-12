using FluentValidation;

namespace Phisio.Application.Assignments.Validators;

public class CreateAssignmentRequestValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty();

        RuleFor(x => x.ExerciseId)
            .NotEmpty();
    }
}
