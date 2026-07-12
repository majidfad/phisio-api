using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientExerciseServiceTests
{
    [Fact]
    public async Task GetExercisesAsync_WhenPatientHasAssignments_ReturnsDoctorNameAndExercises()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor(name: "دکتر رحمانی");
        var patient = ApplicationUserBuilder.Patient();
        var neckStretch = ExerciseBuilder.Create(title: "کشش گردن", videoUrl: "/uploads/exercises/neck.mp4");
        var kneeBend = ExerciseBuilder.Create(title: "خم کردن زانو", videoUrl: "/uploads/exercises/knee.mp4");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var olderAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            neckStretch.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-2));

        var newerAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            kneeBend.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-1));

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [neckStretch, kneeBend],
            userExercises: [olderAssignment, newerAssignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DoctorName.Should().Be("دکتر رحمانی");
        result.Value.Exercises.Should().HaveCount(2);
        result.Value.Exercises[0].Title.Should().Be("خم کردن زانو");
        result.Value.Exercises[0].VideoUrl.Should().Be("/uploads/exercises/knee.mp4");
        result.Value.Exercises[1].Title.Should().Be("کشش گردن");
    }

    [Fact]
    public async Task GetExercisesAsync_WhenNoAssignmentsExist_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DoctorName.Should().Be(doctor.Name);
        result.Value.Exercises.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExercisesAsync_DoesNotReturnOtherPatientAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var otherPatient = ApplicationUserBuilder.Patient(phoneNumber: "+15551112222");
        var exercise = ExerciseBuilder.Create(title: "Private Exercise");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var otherRelationship = DoctorPatientBuilder.Create(doctor.Id, otherPatient.Id);

        var patientAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        var otherAssignment = AssignmentBuilder.Create(
            doctor.Id,
            otherPatient.Id,
            ExerciseBuilder.Create(title: "Other Exercise").ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient, otherPatient],
            exercises: [exercise],
            userExercises: [patientAssignment, otherAssignment],
            doctorPatients: [relationship, otherRelationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Exercises.Should().ContainSingle()
            .Which.Title.Should().Be("Private Exercise");
    }

    [Fact]
    public async Task GetExercisesAsync_ExcludesInactiveAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var activeExercise = ExerciseBuilder.Create(title: "Active Exercise");
        var inactiveExercise = ExerciseBuilder.Create(title: "Inactive Exercise");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var activeAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, activeExercise.ExerciseId);
        var inactiveAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            inactiveExercise.ExerciseId,
            isActive: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [activeExercise, inactiveExercise],
            userExercises: [activeAssignment, inactiveAssignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Value!.Exercises.Should().ContainSingle()
            .Which.Title.Should().Be("Active Exercise");
    }

    [Fact]
    public async Task GetExercisesAsync_ExcludesDisabledExercises()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var enabledExercise = ExerciseBuilder.Create(title: "Enabled Exercise");
        var disabledExercise = ExerciseBuilder.Create(title: "Disabled Exercise");
        disabledExercise.IsEnabled = false;
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var enabledAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, enabledExercise.ExerciseId);
        var disabledAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, disabledExercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [enabledExercise, disabledExercise],
            userExercises: [enabledAssignment, disabledAssignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Value!.Exercises.Should().ContainSingle()
            .Which.Title.Should().Be("Enabled Exercise");
    }

    [Fact]
    public async Task GetExercisesAsync_ExcludesAssignmentsWhenDoctorPatientLinkIsSoftDeleted()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "Hidden Exercise");
        var deletedRelationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [deletedRelationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Value!.Exercises.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExercisesAsync_WhenScheduledDateProvided_ReturnsMatchingAssignmentsOnly()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "Scheduled Exercise");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [relationship],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today),
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    exercise.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: tomorrow),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id, today);

        // Assert
        result.Value!.Exercises.Should().ContainSingle();
        result.Value.Exercises.Single().ScheduledDate.Should().Be(today);
    }
}
