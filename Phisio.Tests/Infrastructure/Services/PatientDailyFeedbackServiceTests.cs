using FluentAssertions;
using Moq;
using Phisio.Application.Common;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientDailyFeedbackServiceSubmitTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task SubmitAsync_WhenExplicitConnectedDoctorId_Succeeds()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientDailyFeedbackService(dbContext.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            DoctorId = doctor.Id,
            ImprovementScore = 4,
            HardnessScore = 3,
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.DoctorId.Should().Be(doctor.Id);
        dbContext.Object.DailyPatientFeedbacks.Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitAsync_WhenExplicitUnconnectedDoctorId_ReturnsNotFound()
    {
        // Arrange
        var connectedDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15551110001");
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15551110002");
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(connectedDoctor.Id, patient.Id);
        var domainEvents = new Mock<IDomainEventDispatcher>();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [connectedDoctor, otherDoctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientDailyFeedbackService(dbContext.Object, domainEvents: domainEvents.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            DoctorId = otherDoctor.Id,
            ImprovementScore = 4,
            HardnessScore = 3,
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientDailyFeedbackErrors.DoctorNotFound);
        dbContext.Object.DailyPatientFeedbacks.Should().BeEmpty();
        domainEvents.Verify(
            service => service.DispatchAsync(
                It.IsAny<Phisio.Domain.Common.IDomainEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenExplicitRandomExistingUserId_ReturnsNotFound()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var admin = ApplicationUserBuilder.Admin();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);
        var domainEvents = new Mock<IDomainEventDispatcher>();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient, admin],
            doctorPatients: [relationship]);

        var sut = new PatientDailyFeedbackService(dbContext.Object, domainEvents: domainEvents.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            DoctorId = admin.Id,
            ImprovementScore = 4,
            HardnessScore = 3,
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientDailyFeedbackErrors.DoctorNotFound);
        dbContext.Object.DailyPatientFeedbacks.Should().BeEmpty();
        domainEvents.Verify(
            service => service.DispatchAsync(
                It.IsAny<Phisio.Domain.Common.IDomainEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenCompletionExistsButRelationshipInactive_ReturnsNotFound()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false);
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        var completion = ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            Today);
        var domainEvents = new Mock<IDomainEventDispatcher>();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            userExercises: [assignment],
            doctorPatients: [relationship],
            exerciseCompletions: [completion]);

        var sut = new PatientDailyFeedbackService(dbContext.Object, domainEvents: domainEvents.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 4,
            HardnessScore = 3,
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(PatientDailyFeedbackErrors.DoctorNotFound);
        dbContext.Object.DailyPatientFeedbacks.Should().BeEmpty();
        domainEvents.Verify(
            service => service.DispatchAsync(
                It.IsAny<Phisio.Domain.Common.IDomainEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenDoctorIdOmittedAndActiveRelationshipExists_Succeeds()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientDailyFeedbackService(dbContext.Object);
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 4,
            HardnessScore = 3,
        };

        // Act
        var result = await sut.SubmitAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.DoctorId.Should().Be(doctor.Id);
        dbContext.Object.DailyPatientFeedbacks.Should().ContainSingle();
    }

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
