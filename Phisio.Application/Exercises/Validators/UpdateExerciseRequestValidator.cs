using FluentValidation;
using Phisio.Application.Exercises;

namespace Phisio.Application.Exercises.Validators;

public class UpdateExerciseRequestValidator : AbstractValidator<UpdateExerciseRequest>
{
    public UpdateExerciseRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.VideoUrl)
            .MaximumLength(500);
    }
}
