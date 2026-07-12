using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientExerciseServiceGetTodayExercisesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    [Fact]
    public async Task GetTodayExercisesAsync_WhenExercisesScheduledForToday_ReturnsThemGroupedByDoctor()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor(name: "دکتر رحمانی");
        var patient = ApplicationUserBuilder.Patient();
        var neckStretch = ExerciseBuilder.Create(title: "کشش گردن");
        var kneeBend = ExerciseBuilder.Create(title: "خم کردن زانو", id: Guid.NewGuid());
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [neckStretch, kneeBend],
            doctorPatients: [relationship],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, neckStretch.ExerciseId, scheduledDate: Today),
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    kneeBend.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: Today),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.DoctorGroups.Should().ContainSingle();
        result.Value.DoctorGroups[0].DoctorName.Should().Be("دکتر رحمانی");
        result.Value.DoctorGroups[0].Exercises.Should().HaveCount(2);
        result.Value.DoctorGroups[0].Exercises.Select(exercise => exercise.Title)
            .Should().BeEquivalentTo(["خم کردن زانو", "کشش گردن"]);
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenExercisesScheduledForPastOrFuture_ReturnsEmpty()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [relationship],
            userExercises:
            [
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    exercise.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: Yesterday),
                AssignmentBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    exercise.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: Tomorrow),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenOnlyPastExerciseExists_DoesNotReturnIt()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "Past Exercise");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [relationship],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Yesterday),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenOnlyFutureExerciseExists_DoesNotReturnIt()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "Future Exercise");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [relationship],
            userExercises:
            [
                AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Tomorrow),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenCompletionExistsForToday_MarksExerciseCompleted()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "کشش گردن");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            completionDate: Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().ContainSingle();
        result.Value.DoctorGroups[0].Exercises.Should().ContainSingle()
            .Which.CompletedToday.Should().BeTrue();
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenCompletionExistsForYesterdayOnly_MarksExerciseUnchecked()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(title: "کشش گردن");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        var yesterdayCompletion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            completionDate: Yesterday);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [yesterdayCompletion]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().ContainSingle();
        result.Value.DoctorGroups[0].Exercises.Should().ContainSingle()
            .Which.CompletedToday.Should().BeFalse();
    }

    [Fact]
    public async Task GetTodayExercisesAsync_WhenMultipleDoctorsHaveTodayExercises_GroupsByDoctor()
    {
        // Arrange
        var doctorRahmani = ApplicationUserBuilder.Doctor(name: "دکتر رحمانی");
        var doctorAhmadi = ApplicationUserBuilder.Doctor(name: "دکتر احمدی", phoneNumber: "+15552222222");
        var patient = ApplicationUserBuilder.Patient();
        var neckStretch = ExerciseBuilder.Create(title: "کشش گردن");
        var kneeBend = ExerciseBuilder.Create(title: "خم کردن زانو", id: Guid.NewGuid());
        var balance = ExerciseBuilder.Create(title: "تمرین تعادل", id: Guid.NewGuid());

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctorRahmani, doctorAhmadi, patient],
            exercises: [neckStretch, kneeBend, balance],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(doctorRahmani.Id, patient.Id),
                DoctorPatientBuilder.Create(doctorAhmadi.Id, patient.Id),
            ],
            userExercises:
            [
                AssignmentBuilder.Create(doctorRahmani.Id, patient.Id, neckStretch.ExerciseId, scheduledDate: Today),
                AssignmentBuilder.Create(
                    doctorRahmani.Id,
                    patient.Id,
                    kneeBend.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: Today),
                AssignmentBuilder.Create(
                    doctorAhmadi.Id,
                    patient.Id,
                    balance.ExerciseId,
                    id: Guid.NewGuid(),
                    scheduledDate: Today),
            ]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetTodayExercisesAsync(patient.Id);

        // Assert
        result.Value!.DoctorGroups.Should().HaveCount(2);
        result.Value.DoctorGroups[0].DoctorName.Should().Be("دکتر احمدی");
        result.Value.DoctorGroups[0].Exercises.Should().ContainSingle()
            .Which.Title.Should().Be("تمرین تعادل");
        result.Value.DoctorGroups[1].DoctorName.Should().Be("دکتر رحمانی");
        result.Value.DoctorGroups[1].Exercises.Should().HaveCount(2);
    }
}
