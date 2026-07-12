using FluentValidation.TestHelper;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Admin.Exercises.Validators;

namespace Phisio.Tests.Application.Validators;

public class CreateExerciseDtoValidatorTests
{
    private readonly CreateExerciseDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateExerciseDto
        {
            Title = "Neck Stretch",
            Description = "Gentle neck mobility exercise.",
            VideoUrl = "https://example.com/neck-stretch",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenRequestIsInvalid_ShouldHaveValidationErrors()
    {
        // Arrange
        var request = new CreateExerciseDto
        {
            Title = string.Empty,
            Description = string.Empty,
            VideoUrl = new string('x', 501),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.VideoUrl);
    }
}
