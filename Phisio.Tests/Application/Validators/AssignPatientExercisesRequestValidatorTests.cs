using FluentValidation.TestHelper;
using Phisio.Application.DoctorPatients;
using Phisio.Application.DoctorPatients.Validators;

namespace Phisio.Tests.Application.Validators;

public class AssignPatientExercisesRequestValidatorTests
{
    private readonly AssignPatientExercisesRequestValidator _validator = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void Validate_WhenExerciseIdsAreEmpty_ReturnsError()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([], [Today]);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ExerciseIds);
    }

    [Fact]
    public void Validate_WhenScheduledDatesAreEmpty_ReturnsError()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([Guid.NewGuid()], []);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ScheduledDates);
    }

    [Fact]
    public void Validate_WhenExerciseIdsAndDatesAreProvided_ReturnsValid()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([Guid.NewGuid()], [Today]);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
