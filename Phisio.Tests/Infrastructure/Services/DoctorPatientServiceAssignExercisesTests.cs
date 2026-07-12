using FluentAssertions;
using Phisio.Application.DoctorPatients;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceAssignExercisesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly DateOnly DayAfterTomorrow = Today.AddDays(2);

    [Fact]
    public async Task AssignExercisesAsync_WhenNoExercisesSelected_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([], [Today]));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientErrors.NoExercisesSelected);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenNoDatesSelected_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([exercise.ExerciseId], []));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientErrors.NoDatesSelected);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenPatientNotLinked_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([exercise.ExerciseId], [Today]));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenSingleDateAndExercise_CreatesAssignmentWithScheduledDate()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var stretch = ExerciseBuilder.Create(title: "Hamstring Stretch");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([stretch.ExerciseId], [Today]));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.AssignedCount.Should().Be(1);
        dbContext.Object.UserExercises.Should().ContainSingle();
        dbContext.Object.UserExercises.Single().ScheduledDate.Should().Be(Today);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenMultipleDatesAndExercises_CreatesCartesianProduct()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var stretch = ExerciseBuilder.Create(title: "Hamstring Stretch");
        var squat = ExerciseBuilder.Create(title: "Squat", id: Guid.NewGuid());
        var dates = new[] { Today, Tomorrow, DayAfterTomorrow };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, squat],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([stretch.ExerciseId, squat.ExerciseId], dates));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.AssignedCount.Should().Be(6);
        dbContext.Object.UserExercises.Should().HaveCount(6);
        dbContext.Object.UserExercises.Select(assignment => assignment.ScheduledDate)
            .Should().BeEquivalentTo(dates.Concat(dates));
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenDuplicateAssignmentExists_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var existingExercise = ExerciseBuilder.Create(title: "Existing Exercise");
        var newExercise = ExerciseBuilder.Create(title: "New Exercise", id: Guid.NewGuid());

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [existingExercise, newExercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises:
            [
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    existingExercise.ExerciseId,
                    scheduledDate: Today),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest(
                [existingExercise.ExerciseId, newExercise.ExerciseId],
                [Today]));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientErrors.DuplicateAssignment);
        dbContext.Object.UserExercises.Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenSameExerciseOnDifferentDates_AllowsAssignment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises:
            [
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    exercise.ExerciseId,
                    scheduledDate: Today),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([exercise.ExerciseId], [Tomorrow]));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.AssignedCount.Should().Be(1);
        dbContext.Object.UserExercises.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenInactiveAssignmentExists_ReactivatesAssignment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var inactiveAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            exercise.ExerciseId,
            isActive: false,
            scheduledDate: Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises: [inactiveAssignment]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([exercise.ExerciseId], [Today]));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.AssignedCount.Should().Be(1);
        dbContext.Object.UserExercises.Should().ContainSingle();
        dbContext.Object.UserExercises.Single().IsActive.Should().BeTrue();
        dbContext.Object.UserExercises.Single().IsEnabled.Should().BeTrue();
        dbContext.Object.UserExercises.Single().ScheduledDate.Should().Be(Today);
    }

    [Fact]
    public async Task AssignExercisesAsync_WhenExerciseIsDisabled_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var disabledExercise = ExerciseBuilder.Create(title: "Disabled Exercise");
        disabledExercise.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [disabledExercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AssignExercisesAsync(
            doctor.Id,
            patient.Id,
            new AssignPatientExercisesRequest([disabledExercise.ExerciseId], [Today]));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientErrors.NoValidExercises);
    }
}
