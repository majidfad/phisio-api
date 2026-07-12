using FluentValidation.TestHelper;
using Phisio.Application.Assignments;
using Phisio.Application.Assignments.Validators;

namespace Phisio.Tests.Application.Validators;

public class CreateAssignmentRequestValidatorTests
{
    private readonly CreateAssignmentRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            ExerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7")
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
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.Empty,
            ExerciseId = Guid.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PatientId);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }
}
