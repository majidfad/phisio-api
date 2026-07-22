using FluentValidation.TestHelper;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;
using Phisio.Application.DoctorPatients.Validators;

namespace Phisio.Tests.Application.Validators;

public class AssignPatientExercisesRequestValidatorTests
{
    private readonly AssignPatientExercisesRequestValidator _validator = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static AssignPatientExerciseItem Item(Guid exerciseId) =>
        new(exerciseId, Sets: 3, Reps: "10", HoldSeconds: null, RestSeconds: null,
            Side: ExerciseSide.NotApplicable, ClinicianNote: null, PatientCue: null);

    [Fact]
    public void Validate_WhenExerciseIdsAreEmpty_ReturnsError()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([], [Today]);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Validate_WhenScheduledDatesAreEmpty_ReturnsError()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([Item(Guid.NewGuid())], []);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ScheduledDates);
    }

    [Fact]
    public void Validate_WhenExerciseIdsAndDatesAreProvided_ReturnsValid()
    {
        // Arrange
        var request = new AssignPatientExercisesRequest([Item(Guid.NewGuid())], [Today]);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
