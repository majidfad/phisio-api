using FluentValidation;
using Phisio.Domain.Enums;

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

        RuleFor(x => x.Instructions)
            .MaximumLength(4000);

        RuleFor(x => x.MediaType)
            .IsInEnum();

        RuleFor(x => x.BodyRegion)
            .IsInEnum();

        RuleFor(x => x.Equipment)
            .IsInEnum();

        RuleFor(x => x.Difficulty)
            .IsInEnum();

        RuleFor(x => x.VideoUrl)
            .MaximumLength(2000)
            .Must((dto, url) => ExerciseMediaUrlRules.IsValid(dto.MediaType, url))
            .WithMessage("Media URL is invalid for the selected media type.")
            .When(x => x.MediaType != ExerciseMediaType.UploadedVideo
                || !string.IsNullOrWhiteSpace(x.VideoUrl));

        RuleFor(x => x.VideoUrl)
            .NotEmpty()
            .When(x => x.MediaType != ExerciseMediaType.UploadedVideo);
    }
}
