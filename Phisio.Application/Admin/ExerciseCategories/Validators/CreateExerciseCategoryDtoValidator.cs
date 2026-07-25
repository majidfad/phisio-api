using FluentValidation;
using Phisio.Application.Admin.ExerciseCategories;

namespace Phisio.Application.Admin.ExerciseCategories.Validators;

public class CreateExerciseCategoryDtoValidator : AbstractValidator<CreateExerciseCategoryDto>
{
    public CreateExerciseCategoryDtoValidator()
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
