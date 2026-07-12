using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.PatientExercises;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientExerciseServiceCompleteTests
{
    [Fact]
    public async Task CompleteExercisesAsync_WhenFirstCompletionToday_CreatesRecord()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] });

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CreatedUserExerciseIds.Should().ContainSingle()
            .Which.Should().Be(assignment.UserExerciseId);
        result.Value.SkippedUserExerciseIds.Should().BeEmpty();
        result.Value.CompletionDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        var stored = await dbContext.Object.ExerciseCompletions.SingleAsync();
        stored.PatientId.Should().Be(patient.Id);
        stored.DoctorId.Should().Be(doctor.Id);
        stored.ExerciseId.Should().Be(exercise.ExerciseId);
        stored.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteExercisesAsync_WhenCompletionAlreadyExistsToday_SkipsDuplicate()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        var existingCompletion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [existingCompletion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] });

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.CreatedUserExerciseIds.Should().BeEmpty();
        result.Value.SkippedUserExerciseIds.Should().ContainSingle()
            .Which.Should().Be(assignment.UserExerciseId);

        (await dbContext.Object.ExerciseCompletions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CompleteExercisesAsync_WhenMultipleExercisesSubmitted_CreatesOnlyNewCompletions()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var stretch = ExerciseBuilder.Create(title: "کشش گردن");
        var knee = ExerciseBuilder.Create(title: "خم کردن زانو");
        var shoulder = ExerciseBuilder.Create(title: "چرخش شانه");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var stretchAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId);
        var kneeAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, knee.ExerciseId);
        var shoulderAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, shoulder.ExerciseId);

        var existingCompletion = ExerciseCompletionBuilder.Create(
            stretchAssignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            stretch.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, knee, shoulder],
            userExercises: [stretchAssignment, kneeAssignment, shoulderAssignment],
            doctorPatients: [relationship],
            exerciseCompletions: [existingCompletion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest
            {
                UserExerciseIds =
                [
                    stretchAssignment.UserExerciseId,
                    kneeAssignment.UserExerciseId,
                    shoulderAssignment.UserExerciseId,
                ],
            });

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.CreatedUserExerciseIds.Should().BeEquivalentTo(
        [
            kneeAssignment.UserExerciseId,
            shoulderAssignment.UserExerciseId,
        ]);
        result.Value.SkippedUserExerciseIds.Should().ContainSingle()
            .Which.Should().Be(stretchAssignment.UserExerciseId);

        (await dbContext.Object.ExerciseCompletions.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task CompleteExercisesAsync_WhenAssignmentBelongsToAnotherPatient_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var otherPatient = ApplicationUserBuilder.Patient(phoneNumber: "+15551112222");
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, otherPatient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, otherPatient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient, otherPatient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientExerciseErrors.AssignmentNotFound);

        (await dbContext.Object.ExerciseCompletions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteExercisesAsync_WhenAnySubmittedIdIsInvalid_RejectsEntireRequest()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest
            {
                UserExerciseIds = [assignment.UserExerciseId, Guid.NewGuid()],
            });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientExerciseErrors.AssignmentNotFound);

        (await dbContext.Object.ExerciseCompletions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetExercisesAsync_IncludesTodayCompletionState()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "کشش گردن");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Value!.Exercises.Should().ContainSingle()
            .Which.IsCompletedToday.Should().BeTrue();
    }

    [Fact]
    public async Task GetExercisesAsync_WhenCompletionExistsForYesterdayOnly_MarksExerciseUncheckedToday()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "کشش گردن");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);
        var yesterdayCompletion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            completionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [yesterdayCompletion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync(patient.Id);

        // Assert
        result.Value!.Exercises.Should().ContainSingle()
            .Which.IsCompletedToday.Should().BeFalse();
    }
}
