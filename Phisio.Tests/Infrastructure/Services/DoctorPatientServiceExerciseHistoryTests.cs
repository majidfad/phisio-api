using FluentAssertions;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceGetExerciseHistoryTests
{
    [Fact]
    public async Task GetExerciseHistoryAsync_WhenPatientNotLinkedToDoctor_ReturnsNotFound()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Other Doctor");
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(otherDoctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenNoAssignments_ReturnsEmptyHistory()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Patient.PatientId.Should().Be(patient.Id);
        result.Value.Patient.PatientName.Should().Be(patient.Name);
        result.Value.Patient.PhoneNumber.Should().Be(patient.PhoneNumber);
        result.Value.Summary.Should().BeEquivalentTo(new PatientExerciseHistorySummaryDto(0, 0, 0, 0));
        result.Value.DailyHistory.Should().BeEmpty();
        result.Value.TotalDays.Should().Be(0);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenNoCompletions_ReturnsZeroAdherenceAndDailyMissedDays()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var stretch = ExerciseBuilder.Create(title: "Neck Stretch");
        var knee = ExerciseBuilder.Create(title: "Knee Bend");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayOne = today.AddDays(-2);
        var dayTwo = today.AddDays(-1);

        var assignments = new[]
        {
            AssignmentBuilder.Create(
                doctor.Id,
                patient.Id,
                stretch.ExerciseId,
                scheduledDate: dayOne),
            AssignmentBuilder.Create(
                doctor.Id,
                patient.Id,
                knee.ExerciseId,
                scheduledDate: dayTwo),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, knee],
            userExercises: assignments,
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Summary.AssignedExerciseCount.Should().Be(2);
        result.Value.Summary.CompletedDaysCount.Should().Be(0);
        result.Value.Summary.MissedDaysCount.Should().Be(2);
        result.Value.Summary.AdherencePercentage.Should().Be(0);
        result.Value.TotalDays.Should().Be(2);
        result.Value.DailyHistory.Should().HaveCount(2);
        result.Value.DailyHistory.Should().OnlyContain(day => !day.IsCompleted && day.CompletedExerciseCount == 0);
        result.Value.DailyHistory.First().Date.Should().Be(dayTwo);
        result.Value.DailyHistory.Last().Date.Should().Be(dayOne);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenMultipleCompletionDays_CalculatesAdherenceAndDetails()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var stretch = ExerciseBuilder.Create(title: "Neck Stretch");
        var knee = ExerciseBuilder.Create(title: "Knee Bend");
        var shoulder = ExerciseBuilder.Create(title: "Shoulder Rotation");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayOne = today.AddDays(-1);

        var stretchToday = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            stretch.ExerciseId,
            scheduledDate: today);
        stretchToday.Sets = 3;
        stretchToday.Reps = "10";
        stretchToday.HoldSeconds = 5;
        stretchToday.RestSeconds = 30;
        stretchToday.Side = ExerciseSide.Left;
        stretchToday.ClinicianNote = "Watch form";
        stretchToday.PatientCue = "Keep spine neutral";

        var kneeToday = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            knee.ExerciseId,
            scheduledDate: today);
        var shoulderToday = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            shoulder.ExerciseId,
            scheduledDate: today);
        var stretchDayOne = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            stretch.ExerciseId,
            scheduledDate: dayOne);
        var kneeDayOne = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            knee.ExerciseId,
            scheduledDate: dayOne);

        var completions = new[]
        {
            ExerciseCompletionBuilder.Create(
                stretchToday.UserExerciseId,
                patient.Id,
                doctor.Id,
                stretch.ExerciseId,
                today),
            ExerciseCompletionBuilder.Create(
                kneeToday.UserExerciseId,
                patient.Id,
                doctor.Id,
                knee.ExerciseId,
                today),
            ExerciseCompletionBuilder.Create(
                stretchDayOne.UserExerciseId,
                patient.Id,
                doctor.Id,
                stretch.ExerciseId,
                dayOne),
            ExerciseCompletionBuilder.Create(
                kneeDayOne.UserExerciseId,
                patient.Id,
                doctor.Id,
                knee.ExerciseId,
                dayOne),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, knee, shoulder],
            userExercises: [stretchToday, kneeToday, shoulderToday, stretchDayOne, kneeDayOne],
            doctorPatients: [relationship],
            exerciseCompletions: completions);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Summary.AssignedExerciseCount.Should().Be(5);
        result.Value.Summary.CompletedDaysCount.Should().Be(2);
        result.Value.Summary.MissedDaysCount.Should().Be(0);
        result.Value.Summary.AdherencePercentage.Should().Be(100);
        result.Value.TotalDays.Should().Be(2);

        var latestDay = result.Value.DailyHistory.First();
        latestDay.Date.Should().Be(today);
        latestDay.CompletedExerciseCount.Should().Be(2);
        latestDay.IsCompleted.Should().BeTrue();
        latestDay.Exercises.Should().HaveCount(3);
        latestDay.Exercises.Single(exercise => exercise.Title == "Shoulder Rotation").IsCompleted.Should().BeFalse();

        var stretchDetail = latestDay.Exercises.Single(exercise => exercise.Title == "Neck Stretch");
        stretchDetail.Sets.Should().Be(3);
        stretchDetail.Reps.Should().Be("10");
        stretchDetail.HoldSeconds.Should().Be(5);
        stretchDetail.RestSeconds.Should().Be(30);
        stretchDetail.Side.Should().Be(ExerciseSide.Left);
        stretchDetail.ClinicianNote.Should().Be("Watch form");
        stretchDetail.PatientCue.Should().Be("Keep spine neutral");

        result.Value.DailyHistory.Should().BeInDescendingOrder(day => day.Date);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_PaginatesDailyHistory()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Neck Stretch");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignments = Enumerable.Range(0, 5)
            .Select(offset => AssignmentBuilder.Create(
                doctor.Id,
                patient.Id,
                exercise.ExerciseId,
                scheduledDate: today.AddDays(-offset)))
            .ToArray();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: assignments,
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id, page: 2, pageSize: 2);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.TotalDays.Should().Be(5);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        result.Value.DailyHistory.Should().HaveCount(2);
        result.Value.DailyHistory[0].Date.Should().Be(today.AddDays(-2));
        result.Value.DailyHistory[1].Date.Should().Be(today.AddDays(-3));
        result.Value.Summary.AssignedExerciseCount.Should().Be(5);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_IgnoresOtherDoctorCompletions()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Other Doctor");
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var otherRelationship = DoctorPatientBuilder.Create(otherDoctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Neck Stretch");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today);
        var otherAssignment = AssignmentBuilder.Create(
            otherDoctor.Id,
            patient.Id,
            exercise.ExerciseId,
            scheduledDate: today);

        var completions = new[]
        {
            ExerciseCompletionBuilder.Create(
                assignment.UserExerciseId,
                patient.Id,
                doctor.Id,
                exercise.ExerciseId,
                today),
            ExerciseCompletionBuilder.Create(
                otherAssignment.UserExerciseId,
                patient.Id,
                otherDoctor.Id,
                exercise.ExerciseId,
                today),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            exercises: [exercise],
            userExercises: [assignment, otherAssignment],
            doctorPatients: [relationship, otherRelationship],
            exerciseCompletions: completions);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Summary.CompletedDaysCount.Should().Be(1);
        result.Value.DailyHistory.First().CompletedExerciseCount.Should().Be(1);
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenDailyFeedbackExists_IncludesScoreAndComment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Knee Bend");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            today);
        var feedback = DailyPatientFeedbackBuilder.Create(
            patient.Id,
            doctor.Id,
            improvementScore: 4,
            comment: "امروز درد زانو کمتر بود.",
            feedbackDate: today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion],
            dailyPatientFeedbacks: [feedback]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        var todayHistory = result.Value!.DailyHistory.First(day => day.Date == today);
        todayHistory.ImprovementScore.Should().Be(4);
        todayHistory.Comment.Should().Be("امروز درد زانو کمتر بود.");
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenNoFeedback_ReturnsNullScoreAndComment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Knee Bend");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        var todayHistory = result.Value!.DailyHistory.First(day => day.Date == today);
        todayHistory.ImprovementScore.Should().BeNull();
        todayHistory.Comment.Should().BeNull();
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenFeedbackHasScoreWithoutComment_ReturnsNullComment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Knee Bend");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            today);
        var feedback = DailyPatientFeedbackBuilder.Create(
            patient.Id,
            doctor.Id,
            improvementScore: 3,
            comment: null,
            feedbackDate: today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion],
            dailyPatientFeedbacks: [feedback]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        var todayHistory = result.Value!.DailyHistory.First(day => day.Date == today);
        todayHistory.ImprovementScore.Should().Be(3);
        todayHistory.Comment.Should().BeNull();
    }

    [Fact]
    public async Task GetExerciseHistoryAsync_WhenFeedbackFromOtherDoctor_IsNotIncluded()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Other Doctor");
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var otherRelationship = DoctorPatientBuilder.Create(otherDoctor.Id, patient.Id);
        var exercise = ExerciseBuilder.Create(title: "Knee Bend");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            today);
        var otherDoctorFeedback = DailyPatientFeedbackBuilder.Create(
            patient.Id,
            otherDoctor.Id,
            improvementScore: 5,
            comment: "Should not appear",
            feedbackDate: today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship, otherRelationship],
            exerciseCompletions: [completion],
            dailyPatientFeedbacks: [otherDoctorFeedback]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        var todayHistory = result.Value!.DailyHistory.First(day => day.Date == today);
        todayHistory.ImprovementScore.Should().BeNull();
        todayHistory.Comment.Should().BeNull();
    }
}
