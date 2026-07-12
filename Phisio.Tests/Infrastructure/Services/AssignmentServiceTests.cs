using FluentAssertions;
using Moq;
using Phisio.Application.Admin.Assignments;
using Phisio.Application.Assignments;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class AssignmentServiceGetPatientAssignmentsTests
{
    [Fact]
    public async Task GetMyAssignmentsAsync_WhenNoActiveAssignmentsExist_ReturnsEmptyList()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [patient]);
        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetMyAssignmentsAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyAssignmentsAsync_WhenMultipleActiveAssignmentsExist_ReturnsAssignments()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var stretch = ExerciseBuilder.Create(title: "Neck Stretch");
        var roll = ExerciseBuilder.Create(title: "Shoulder Roll");

        var olderAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            stretch.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-2));

        var newerAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            roll.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-1));

        var inactiveAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            ExerciseBuilder.Create(title: "Inactive Exercise").ExerciseId,
            isActive: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            exercises: [stretch, roll],
            userExercises: [olderAssignment, newerAssignment, inactiveAssignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetMyAssignmentsAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(2);
        result.Value.Select(dto => dto.ExerciseTitle).Should().ContainInOrder("Shoulder Roll", "Neck Stretch");
        result.Value.Should().OnlyContain(dto => dto.IsActive);
    }
}

public class AssignmentServiceGetDoctorAssignmentsTests
{
    [Fact]
    public async Task GetByPatientIdAsync_WhenDoctorHasNoLinkedPatient_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetByPatientIdAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found or is not linked to this doctor.");
    }

    [Fact]
    public async Task GetByPatientIdAsync_WhenMultipleAssignmentsExist_ReturnsOrderedAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var stretch = ExerciseBuilder.Create(title: "Ankle Circle");
        var roll = ExerciseBuilder.Create(title: "Knee Flex");
        var doctorPatient = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var olderAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            stretch.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-3));

        var newerAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            roll.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-1),
            isActive: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, roll],
            userExercises: [olderAssignment, newerAssignment],
            doctorPatients: [doctorPatient]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetByPatientIdAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(2);
        result.Value.Select(dto => dto.ExerciseTitle).Should().ContainInOrder("Knee Flex", "Ankle Circle");
        result.Value.Should().Contain(dto => !dto.IsActive);
    }
}

public class AssignmentServiceCreateTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ReturnsSuccess()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "Hamstring Stretch");
        var request = new CreateAssignmentRequest
        {
            PatientId = patient.Id,
            ExerciseId = exercise.ExerciseId
        };
        var doctorPatient = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [doctorPatient]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DoctorId.Should().Be(doctor.Id);
        result.Value.PatientId.Should().Be(patient.Id);
        result.Value.ExerciseId.Should().Be(exercise.ExerciseId);
        result.Value.ExerciseTitle.Should().Be(exercise.Title);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var exercise = ExerciseBuilder.Create();
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.NewGuid(),
            ExerciseId = exercise.ExerciseId
        };

        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor], exercises: [exercise]);
        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found or is not linked to this doctor.");
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseNotFound_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var doctorPatient = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var request = new CreateAssignmentRequest
        {
            PatientId = patient.Id,
            ExerciseId = Guid.NewGuid()
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [doctorPatient]);
        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Exercise not found.");
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateActiveAssignmentExists_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var existingAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        var request = new CreateAssignmentRequest
        {
            PatientId = patient.Id,
            ExerciseId = exercise.ExerciseId
        };

        var doctorPatient = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [existingAssignment],
            doctorPatients: [doctorPatient]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("This exercise is already actively assigned to the patient.");
    }

    [Fact]
    public async Task CreateAsync_WhenDoctorPatientLinkIsSoftDeleted_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var request = new CreateAssignmentRequest
        {
            PatientId = patient.Id,
            ExerciseId = exercise.ExerciseId,
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false)]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found or is not linked to this doctor.");
    }
}

public class AssignmentServiceDeactivateTests
{
    [Fact]
    public async Task DeactivateAsync_WhenAssignmentExists_ReturnsSuccess()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id, assignment.UserExerciseId);

        // Assert
        result.Succeeded.Should().BeTrue();
        dbContext.Object.UserExercises.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_WhenAssignmentNotFound_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id, Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Assignment not found.");
    }

    [Fact]
    public async Task DeactivateAsync_WhenAssignmentBelongsToAnotherDoctor_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var assignment = AssignmentBuilder.Create(otherDoctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            exercises: [exercise],
            userExercises: [assignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id, assignment.UserExerciseId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Assignment not found.");
    }

    [Fact]
    public async Task DeactivateAsync_WhenAssignmentAlreadyInactive_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var assignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            exercise.ExerciseId,
            isActive: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id, assignment.UserExerciseId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Assignment is already inactive.");
    }
}

public class AssignmentServiceGetReportTests
{
    [Fact]
    public async Task GetReportAsync_GroupsActiveAssignmentsByPatientAndDoctor()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor(name: "Dr. Ali");
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Dr. Sara", phoneNumber: "+15550000002");
        var patient = ApplicationUserBuilder.Patient(name: "Reza Patient");
        var stretch = ExerciseBuilder.Create(title: "Hamstring Stretch");
        var squat = ExerciseBuilder.Create(title: "Squat", id: Guid.NewGuid());
        var plank = ExerciseBuilder.Create(title: "Plank", id: Guid.NewGuid());

        var assignments = new[]
        {
            AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId),
            AssignmentBuilder.Create(doctor.Id, patient.Id, squat.ExerciseId),
            AssignmentBuilder.Create(otherDoctor.Id, patient.Id, plank.ExerciseId),
            AssignmentBuilder.Create(
                otherDoctor.Id,
                patient.Id,
                stretch.ExerciseId,
                isActive: false),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            exercises: [stretch, squat, plank],
            userExercises: assignments);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetReportAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(
        [
            new AssignmentReportDto(
                "Reza Patient",
                "Dr. Ali",
                ["Hamstring Stretch", "Squat"]),
            new AssignmentReportDto(
                "Reza Patient",
                "Dr. Sara",
                ["Plank"]),
        ],
        options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetReportAsync_ExcludesSoftDeletedAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var disabledAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        disabledAssignment.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [disabledAssignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetReportAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_ExcludesDisabledExercises()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        exercise.IsEnabled = false;
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment]);

        var sut = new AssignmentService(dbContext.Object);

        // Act
        var result = await sut.GetReportAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WhenNoAssignmentsExist_ReturnsEmptyList()
    {
        // Arrange
        var sut = new AssignmentService(AppDbContextMockFactory.CreateMock().Object);

        // Act
        var result = await sut.GetReportAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
