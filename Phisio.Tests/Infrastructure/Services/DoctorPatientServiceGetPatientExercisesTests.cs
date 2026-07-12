using FluentAssertions;
using Phisio.Application.DoctorPatients;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceGetPatientExercisesTests
{
    [Fact]
    public async Task GetPatientExercisesAsync_WhenPatientNotLinked_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }

    [Fact]
    public async Task GetPatientExercisesAsync_WhenPatientLinkedWithNoExercises_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientExercisesAsync_WhenPatientHasAssignments_ReturnsExerciseDetailsOrderedByAssignedAtDescending()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var olderExercise = ExerciseBuilder.Create(title: "Older Exercise", videoUrl: "https://example.com/older.mp4");
        var newerExercise = ExerciseBuilder.Create(title: "Newer Exercise", videoUrl: "https://example.com/newer.mp4");
        var olderAssignedAt = DateTime.UtcNow.AddDays(-5);
        var newerAssignedAt = DateTime.UtcNow.AddDays(-1);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [olderExercise, newerExercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, olderExercise.ExerciseId, assignedAt: olderAssignedAt),
                AssignmentBuilder.Create(doctor.Id, patient.Id, newerExercise.ExerciseId, assignedAt: newerAssignedAt),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(exercise => exercise.ExerciseName).Should()
            .ContainInOrder("Newer Exercise", "Older Exercise");
        result.Value.First().VideoUrl.Should().Be("https://example.com/newer.mp4");
        result.Value.First().ExerciseId.Should().Be(newerExercise.ExerciseId);
        result.Value.First().AssignedAt.Should().BeCloseTo(newerAssignedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetPatientExercisesAsync_ExcludesInactiveAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var activeExercise = ExerciseBuilder.Create(title: "Active Exercise");
        var inactiveExercise = ExerciseBuilder.Create(title: "Inactive Exercise", id: Guid.NewGuid());

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [activeExercise, inactiveExercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, activeExercise.ExerciseId),
                AssignmentBuilder.Create(doctor.Id, patient.Id, inactiveExercise.ExerciseId, isActive: false),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().ExerciseName.Should().Be("Active Exercise");
    }

    [Fact]
    public async Task GetPatientExercisesAsync_ExcludesAssignmentsFromOtherDoctors()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        var patient = ApplicationUserBuilder.Patient();
        var doctorExercise = ExerciseBuilder.Create(title: "Doctor Exercise");
        var otherExercise = ExerciseBuilder.Create(title: "Other Doctor Exercise", id: Guid.NewGuid());

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            exercises: [doctorExercise, otherExercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, doctorExercise.ExerciseId),
                AssignmentBuilder.Create(otherDoctor.Id, patient.Id, otherExercise.ExerciseId),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().ExerciseName.Should().Be("Doctor Exercise");
    }

    [Fact]
    public async Task GetPatientExercisesAsync_WhenDoctorPatientLinkIsSoftDeleted_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false)],
            userExercises: [AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId)]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientExercisesAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }
}
