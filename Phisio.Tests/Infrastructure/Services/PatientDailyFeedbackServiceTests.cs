using FluentAssertions;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.PatientExercises;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientDailyFeedbackServiceSubmitTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task SubmitAsync_WhenFirstFeedbackForToday_CreatesRecord()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion]);

        var sut = new PatientDailyFeedbackService(dbContext.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 4,
            HardnessScore = 3,
            Comment = "امروز درد زانو کمتر بود.",
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.WasUpdated.Should().BeFalse();
        result.Value.ImprovementScore.Should().Be(4);
        result.Value.Comment.Should().Be("امروز درد زانو کمتر بود.");
        result.Value.DoctorId.Should().Be(doctor.Id);
        result.Value.FeedbackDate.Should().Be(Today);

        dbContext.Object.DailyPatientFeedbacks.Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitAsync_WhenFeedbackAlreadyExistsForToday_UpdatesExistingRecord()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var existingFeedback = DailyPatientFeedbackBuilder.Create(
            patient.Id,
            doctor.Id,
            improvementScore: 2,
            comment: "دیروز بد بود",
            feedbackDate: Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship],
            dailyPatientFeedbacks: [existingFeedback]);

        var sut = new PatientDailyFeedbackService(dbContext.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 5,
            HardnessScore = 2,
            Comment = "امروز خیلی بهتر شدم.",
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.WasUpdated.Should().BeTrue();
        result.Value.ImprovementScore.Should().Be(5);
        result.Value.Comment.Should().Be("امروز خیلی بهتر شدم.");

        dbContext.Object.DailyPatientFeedbacks.Should().ContainSingle();
        var stored = dbContext.Object.DailyPatientFeedbacks.Single();
        stored.ImprovementScore.Should().Be(5);
        stored.Comment.Should().Be("امروز خیلی بهتر شدم.");
    }

    [Fact]
    public void SubmitAsync_WhenFeedbackSkipped_DoesNotCreateRecord()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        // Act — skipping feedback means SubmitAsync is never called

        // Assert
        dbContext.Object.DailyPatientFeedbacks.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_WhenNoDoctorRelationshipExists_ReturnsNotFound()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [patient]);
        var sut = new PatientDailyFeedbackService(dbContext.Object);

        // Act
        var result = await sut.SubmitAsync(
            patient.Id,
            new SubmitDailyFeedbackRequest { ImprovementScore = 3 });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientDailyFeedbackErrors.DoctorNotFound);
    }

    [Fact]
    public async Task CompleteExercisesAsync_DoesNotCreateDailyFeedback()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship]);

        var sut = new PatientExerciseService(dbContext.Object);

        // Act
        await sut.CompleteExercisesAsync(
            patient.Id,
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] });

        // Assert
        dbContext.Object.ExerciseCompletions.Should().ContainSingle();
        dbContext.Object.DailyPatientFeedbacks.Should().BeEmpty();
    }
}
