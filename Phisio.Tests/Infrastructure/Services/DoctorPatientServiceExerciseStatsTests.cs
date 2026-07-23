using FluentAssertions;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceExerciseStatsTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly TwoDaysAgo = Today.AddDays(-2);

    [Fact]
    public async Task GetExerciseStatsAsync_WhenPatientNotLinked_ReturnsFailure()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.GetExerciseStatsAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }

    [Fact]
    public async Task GetExerciseStatsAsync_ComputesSummaryDailyWeeklyAndExercises()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var stretch = ExerciseBuilder.Create(title: "Stretch", createdByDoctorId: doctor.Id);
        var squat = ExerciseBuilder.Create(title: "Squat", createdByDoctorId: doctor.Id);

        var a1 = AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId, scheduledDate: TwoDaysAgo);
        var a2 = AssignmentBuilder.Create(doctor.Id, patient.Id, squat.ExerciseId, scheduledDate: TwoDaysAgo);
        var a3 = AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId, scheduledDate: Yesterday);
        var a4 = AssignmentBuilder.Create(doctor.Id, patient.Id, squat.ExerciseId, scheduledDate: Today);

        var completions = new[]
        {
            ExerciseCompletionBuilder.Create(a1.UserExerciseId, patient.Id, doctor.Id, stretch.ExerciseId, TwoDaysAgo),
            ExerciseCompletionBuilder.Create(a3.UserExerciseId, patient.Id, doctor.Id, stretch.ExerciseId, Yesterday),
        };

        var feedback = DailyPatientFeedbackBuilder.Create(
            patient.Id,
            doctor.Id,
            improvementScore: 4,
            hardnessScore: 2,
            feedbackDate: Yesterday);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, squat],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises: [a1, a2, a3, a4],
            exerciseCompletions: completions,
            dailyPatientFeedbacks: [feedback]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.GetExerciseStatsAsync(
            doctor.Id,
            patient.Id,
            from: TwoDaysAgo,
            to: Today);

        result.Succeeded.Should().BeTrue();
        var stats = result.Value!;
        stats.From.Should().Be(TwoDaysAgo);
        stats.To.Should().Be(Today);
        stats.Summary.ScheduledDays.Should().Be(3);
        stats.Summary.CompletedDays.Should().Be(2);
        stats.Summary.MissedDays.Should().Be(1);
        stats.Summary.AssignedExerciseCount.Should().Be(4);
        stats.Summary.CompletedExerciseCount.Should().Be(2);
        stats.Summary.AverageImprovementScore.Should().Be(4);
        stats.Summary.AverageHardnessScore.Should().Be(2);
        stats.Summary.FeedbackDayCount.Should().Be(1);
        stats.Daily.Should().HaveCount(3);
        stats.Weekly.Should().NotBeEmpty();
        stats.Exercises.Should().HaveCount(2);
        stats.Exercises.First().Title.Should().Be("Squat");
        stats.Exercises.First().CompletionPercentage.Should().Be(0);
    }
}
