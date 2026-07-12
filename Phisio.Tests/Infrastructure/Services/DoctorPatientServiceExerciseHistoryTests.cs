using FluentAssertions;
using Phisio.Application.DoctorPatients;
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
        var assignedAt = DateTime.UtcNow.AddDays(-2);

        var assignments = new[]
        {
            AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId, assignedAt: assignedAt),
            AssignmentBuilder.Create(doctor.Id, patient.Id, knee.ExerciseId, assignedAt: assignedAt),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, knee],
            userExercises: assignments,
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDay = DateOnly.FromDateTime(assignedAt);
        var expectedAssignedDays = today.DayNumber - firstDay.DayNumber + 1;

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Summary.AssignedExerciseCount.Should().Be(2);
        result.Value.Summary.CompletedDaysCount.Should().Be(0);
        result.Value.Summary.MissedDaysCount.Should().Be(expectedAssignedDays);
        result.Value.Summary.AdherencePercentage.Should().Be(0);
        result.Value.DailyHistory.Should().HaveCount(expectedAssignedDays);
        result.Value.DailyHistory.Should().OnlyContain(day => !day.IsCompleted && day.CompletedExerciseCount == 0);
        result.Value.DailyHistory.First().Date.Should().Be(today);
        result.Value.DailyHistory.Last().Date.Should().Be(firstDay);
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
        var assignedAt = DateTime.UtcNow.AddDays(-9);

        var stretchAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, stretch.ExerciseId, assignedAt: assignedAt);
        var kneeAssignment = AssignmentBuilder.Create(doctor.Id, patient.Id, knee.ExerciseId, assignedAt: assignedAt);
        var shoulderAssignment = AssignmentBuilder.Create(
            doctor.Id,
            patient.Id,
            shoulder.ExerciseId,
            assignedAt: assignedAt);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var completedDates = Enumerable.Range(0, 8)
            .Select(offset => today.AddDays(-offset))
            .ToList();

        var completions = completedDates
            .SelectMany(date => new[]
            {
                ExerciseCompletionBuilder.Create(
                    stretchAssignment.UserExerciseId,
                    patient.Id,
                    doctor.Id,
                    stretch.ExerciseId,
                    date),
                ExerciseCompletionBuilder.Create(
                    kneeAssignment.UserExerciseId,
                    patient.Id,
                    doctor.Id,
                    knee.ExerciseId,
                    date),
            })
            .ToList();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [stretch, knee, shoulder],
            userExercises: [stretchAssignment, kneeAssignment, shoulderAssignment],
            doctorPatients: [relationship],
            exerciseCompletions: completions);

        var sut = new DoctorPatientService(dbContext.Object);
        var firstDay = DateOnly.FromDateTime(assignedAt);
        var assignedDays = today.DayNumber - firstDay.DayNumber + 1;

        // Act
        var result = await sut.GetExerciseHistoryAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Summary.AssignedExerciseCount.Should().Be(3);
        result.Value.Summary.CompletedDaysCount.Should().Be(8);
        result.Value.Summary.MissedDaysCount.Should().Be(assignedDays - 8);
        result.Value.Summary.AdherencePercentage.Should().Be((int)Math.Round(8 * 100.0 / assignedDays));

        var latestDay = result.Value.DailyHistory.First();
        latestDay.Date.Should().Be(today);
        latestDay.CompletedExerciseCount.Should().Be(2);
        latestDay.IsCompleted.Should().BeTrue();
        latestDay.Exercises.Should().HaveCount(3);
        latestDay.Exercises.Single(exercise => exercise.Title == "Shoulder Rotation").IsCompleted.Should().BeFalse();

        result.Value.DailyHistory.Should().BeInDescendingOrder(day => day.Date);
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
        var assignedAt = DateTime.UtcNow.AddDays(-1);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var otherAssignment = AssignmentBuilder.Create(otherDoctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
        var assignedAt = DateTime.UtcNow.AddDays(-1);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var assignedAt = DateTime.UtcNow.AddDays(-1);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var assignedAt = DateTime.UtcNow.AddDays(-1);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var assignedAt = DateTime.UtcNow.AddDays(-1);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, assignedAt: assignedAt);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
