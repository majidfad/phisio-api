using FluentValidation.TestHelper;
using Phisio.Application.Exercises;
using Phisio.Application.Exercises.Validators;

namespace Phisio.Tests.Application.Validators;

public class UpdateExerciseRequestValidatorTests
{
    private readonly UpdateExerciseRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new UpdateExerciseRequest
        {
            Title = "Updated Neck Stretch",
            Description = "Updated description.",
            VideoUrl = "https://example.com/updated",
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
        var request = new UpdateExerciseRequest
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
