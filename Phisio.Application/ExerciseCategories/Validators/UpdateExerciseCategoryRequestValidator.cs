using FluentValidation;

namespace Phisio.Application.ExerciseCategories.Validators;

public class UpdateExerciseCategoryRequestValidator : AbstractValidator<UpdateExerciseCategoryRequest>
{
    public UpdateExerciseCategoryRequestValidator()
    {
        RuleFor(x => x.NameFa)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.NameEn)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
