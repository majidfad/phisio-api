using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end coverage for patient daily feedback submission and doctor visibility.
///
/// Architecture notes (current implementation):
/// - Single patient endpoint: POST /api/patient/daily-feedback (PatientOnly).
/// - No patient GET endpoint; submit response returns the persisted feedback.
/// - FeedbackDate is always UTC today (not supplied by the client).
/// - Upsert key: (PatientId, DoctorId, FeedbackDate) — second submit same day updates in place.
/// - DoctorId resolves from request, today's completion, or latest approved DoctorPatient link,
///   then must match an active approved DoctorPatient relationship before save.
/// - Doctor views feedback via GetPatientExerciseHistory / GetPatientExerciseStats (doctor-only).
/// - Submit notifies doctor (NotificationType.DailyFeedbackReceived).
/// </summary>
public sealed class PatientDailyFeedbackIntegrationTests
{
    // 1. Patient submits daily feedback successfully.
    [Fact]
    public async Task SubmitFeedback_WhenPatientHasDoctorRelationship_ReturnsOkAndPersists()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var request = ExerciseManagementTestHelpers.ValidFeedbackRequest(
            doctorId: scenario.Doctor.Id,
            improvementScore: 4,
            hardnessScore: 2,
            comment: "Knee pain reduced.");

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            request);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject;
        body.WasUpdated.Should().BeFalse();
        body.ImprovementScore.Should().Be(4);
        body.HardnessScore.Should().Be(2);
        body.Comment.Should().Be("Knee pain reduced.");
        body.DoctorId.Should().Be(scenario.Doctor.Id);
        body.PatientId.Should().Be(scenario.Patient.Id);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(1);
    }

    // 2. Feedback persisted with correct PatientId and date.
    [Fact]
    public async Task SubmitFeedback_PersistsPatientIdAndTodayFeedbackDate()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        host.DbContext.ChangeTracker.Clear();
        var feedback = await host.DbContext.DailyPatientFeedbacks.SingleAsync();
        feedback.PatientId.Should().Be(scenario.Patient.Id);
        feedback.DoctorId.Should().Be(scenario.Doctor.Id);
        feedback.FeedbackDate.Should().Be(today);
        feedback.IsEnabled.Should().BeTrue();
    }

    // 3. Patient can update/upsert feedback for the same day.
    [Fact]
    public async Task SubmitFeedback_WhenFeedbackExistsForToday_UpdatesExistingRecord()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var existing = await ExerciseManagementTestHostSeeder.SeedFeedbackAsync(
            host,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            improvementScore: 2,
            comment: "Old comment");

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 5,
                hardnessScore: 4,
                comment: "Updated comment"));

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject;
        body.WasUpdated.Should().BeTrue();
        body.DailyPatientFeedbackId.Should().Be(existing.DailyPatientFeedbackId);

        host.DbContext.ChangeTracker.Clear();
        var stored = await host.DbContext.DailyPatientFeedbacks.SingleAsync();
        stored.ImprovementScore.Should().Be(5);
        stored.HardnessScore.Should().Be(4);
        stored.Comment.Should().Be("Updated comment");
    }

    // 4. Duplicate submit follows upsert rules — single row, WasUpdated on second call.
    [Fact]
    public async Task SubmitFeedback_WhenSubmittedTwiceSameDay_DoesNotCreateDuplicateRows()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var request = ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id);

        var first = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(controller, request);
        first.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject
            .WasUpdated.Should().BeFalse();

        var second = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 3,
                comment: "Second submit"));

        second.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject
            .WasUpdated.Should().BeTrue();

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(1);
    }

    // 5. Patient cannot submit feedback for another patient (claims-scoped PatientId).
    [Fact]
    public async Task SubmitFeedback_AlwaysPersistsAuthenticatedPatientId_NotAnotherPatient()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);

        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        host.DbContext.ChangeTracker.Clear();
        var feedback = await host.DbContext.DailyPatientFeedbacks.SingleAsync();
        feedback.PatientId.Should().Be(scenario.Patient.Id);
        feedback.PatientId.Should().NotBe(scenario.OtherPatient.Id);
    }

    // 6. Doctor cannot submit patient feedback.
    [Fact]
    public async Task SubmitFeedback_PatientOnlyPolicy_RejectsDoctorRole()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var doctor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Doctor)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();

        typeof(PatientDailyFeedbackController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.PatientOnly);
    }

    [Fact]
    public async Task SubmitFeedback_WhenDoctorUserIdUsedWithoutRelationship_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorAsPatientController = host.CreatePatientDailyFeedbackController(
            scenario.Doctor.Id,
            RoleNames.Doctor);

        // No explicit DoctorId: service resolves from completion/relationship; doctor has neither as patient.
        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            doctorAsPatientController,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                improvementScore: 3,
                hardnessScore: 3));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitFeedback_WhenExplicitConnectedDoctorId_ReturnsOk()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        result.Should().BeOfType<OkObjectResult>();
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitFeedback_WhenExplicitUnconnectedDoctorId_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.OtherDoctor.Id));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
        notifications.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitFeedback_WhenExplicitRandomExistingUserId_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Admin.Id));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
        notifications.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitFeedback_WhenCompletionExistsButRelationshipInactive_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var today = ExerciseManagementTestHelpers.Today;

        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today);
        host.DbContext.ExerciseCompletions.Add(ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            scenario.DoctorExercise.ExerciseId,
            today));

        var link = await host.DbContext.DoctorPatients.SingleAsync(
            dp => dp.DoctorId == scenario.Doctor.Id && dp.PatientId == scenario.Patient.Id);
        link.IsEnabled = false;
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: null));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
        notifications.Notifications.Should().BeEmpty();
    }

    // 7. Admin authorization follows PatientOnly policy.
    [Fact]
    public async Task SubmitFeedback_PatientOnlyPolicy_RejectsAdminRole()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
    }

    // 8. Anonymous users rejected.
    [Fact]
    public async Task SubmitFeedback_PatientOnlyPolicy_RejectsAnonymousUser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
    }

    // 9. Missing user claims rejected safely.
    [Fact]
    public async Task SubmitFeedback_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreatePatientDailyFeedbackController(userId: null, RoleNames.Patient);

        var result = await controller.SubmitFeedback(
            ExerciseManagementTestHelpers.ValidFeedbackRequest(),
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
    }

    // 10. Invalid feedback values rejected by validation.
    [Fact]
    public async Task SubmitFeedback_WhenScoresAreOutOfRange_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 0,
                hardnessScore: 6));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
    }

    // 11. Required field validation (scores must be 1–5).
    [Fact]
    public async Task SubmitFeedback_WhenImprovementScoreMissingFromValidRange_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            new SubmitDailyFeedbackRequest
            {
                DoctorId = scenario.Doctor.Id,
                ImprovementScore = 0,
                HardnessScore = 3,
            });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitFeedback_WhenCommentTooLong_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                comment: new string('x', 1001)));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // 12. Feedback date/day rules — always UTC today; API has no date field.
    [Fact]
    public async Task SubmitFeedback_AlwaysUsesUtcToday_AsFeedbackDate()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var today = ExerciseManagementTestHelpers.Today;

        // Seed feedback for yesterday — today's submit must create a new row, not update yesterday.
        await ExerciseManagementTestHostSeeder.SeedFeedbackAsync(
            host,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            feedbackDate: today.AddDays(-1));

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        host.DbContext.ChangeTracker.Clear();
        var feedbacks = await host.DbContext.DailyPatientFeedbacks.ToListAsync();
        feedbacks.Should().HaveCount(2);
        feedbacks.Should().Contain(f => f.FeedbackDate == today);
        feedbacks.Should().Contain(f => f.FeedbackDate == today.AddDays(-1));
    }

    // 13. No patient GET endpoint — submit response returns own feedback.
    [Fact]
    public void PatientDailyFeedback_HasNoGetEndpoint_SubmitResponseIsSourceOfTruth()
    {
        typeof(PatientDailyFeedbackController)
            .GetMethods()
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitFeedback_ResponseReturnsOwnPersistedFeedbackFields()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 3,
                hardnessScore: 2,
                comment: "My feedback"));

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject;
        body.PatientId.Should().Be(scenario.Patient.Id);
        body.ImprovementScore.Should().Be(3);
        body.Comment.Should().Be("My feedback");
    }

    // 14. Patient cannot retrieve another patient's feedback (no GET; DB isolation via claims).
    [Fact]
    public async Task SubmitFeedback_AsPatientA_DoesNotCreateFeedbackForPatientB()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);

        var controllerA = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controllerA,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DailyPatientFeedbacks
            .CountAsync(f => f.PatientId == scenario.OtherPatient.Id))
            .Should().Be(0);
    }

    // 15. Doctor retrieves feedback for connected patient via exercise history.
    [Fact]
    public async Task GetPatientExerciseHistory_IncludesTodayFeedbackForConnectedPatient()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today);

        var patientController = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            patientController,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 4,
                hardnessScore: 3,
                comment: "Doctor-visible comment"));

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.GetPatientExerciseHistory(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        var history = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientExerciseHistoryResponse>().Subject;
        var todayEntry = history.DailyHistory.Should().ContainSingle(d => d.Date == today).Subject;
        todayEntry.ImprovementScore.Should().Be(4);
        todayEntry.HardnessScore.Should().Be(3);
        todayEntry.Comment.Should().Be("Doctor-visible comment");
    }

    // 16. Doctor cannot access another doctor's patient feedback.
    [Fact]
    public async Task GetPatientExerciseHistory_WhenDoctorNotLinkedToPatient_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var patientController = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            patientController,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        var otherDoctorController = host.CreateDoctorPatientsController(scenario.OtherDoctor.Id);
        var result = await otherDoctorController.GetPatientExerciseHistory(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // 17. Relationship / doctor resolution checks.
    [Fact]
    public async Task SubmitFeedback_WhenNoDoctorRelationshipExists_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15559990001");
        host.DbContext.Users.Add(patient);
        var patientRole = await host.DbContext.Roles.SingleAsync(r => r.Name == RoleNames.Patient);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = patient.Id,
            RoleId = patientRole.Id,
        });
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDailyFeedbackController(patient.Id);
        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(improvementScore: 3, hardnessScore: 3));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.DailyPatientFeedbacks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitFeedback_WhenDoctorIdOmitted_ResolvesFromApprovedRelationship()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: null));

        result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject
            .DoctorId.Should().Be(scenario.Doctor.Id);
    }

    [Fact]
    public async Task SubmitFeedback_WhenDoctorIdOmitted_ResolvesFromTodaysCompletionBeforeRelationship()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.OtherDoctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId);

        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today);
        host.DbContext.ExerciseCompletions.Add(ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            scenario.DoctorExercise.ExerciseId,
            today));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: null));

        result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject
            .DoctorId.Should().Be(scenario.Doctor.Id);
    }

    // 18. Database state and foreign keys.
    [Fact]
    public async Task SubmitFeedback_PersistsForeignKeysAndUniqueIndexShape()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        host.DbContext.ChangeTracker.Clear();
        var feedback = await host.DbContext.DailyPatientFeedbacks.SingleAsync();
        feedback.PatientId.Should().Be(scenario.Patient.Id);
        feedback.DoctorId.Should().Be(scenario.Doctor.Id);

        var entityType = host.DbContext.Model.FindEntityType(typeof(DailyPatientFeedback));
        entityType.Should().NotBeNull();
        entityType!.GetForeignKeys()
            .SelectMany(fk => fk.Properties.Select(p => p.Name))
            .Should().Contain("PatientId");
        entityType.GetForeignKeys()
            .SelectMany(fk => fk.Properties.Select(p => p.Name))
            .Should().Contain("DoctorId");

        var expectedUnique = new[] { "ClinicId", "DoctorId", "FeedbackDate", "PatientId" };
        entityType.GetIndexes()
            .Where(index => index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).OrderBy(n => n).ToArray())
            .Should().ContainEquivalentOf(expectedUnique.OrderBy(n => n).ToArray());
    }

    // 19. Save failure rollback.
    [Fact]
    public async Task SubmitFeedback_WhenSaveFails_LeavesNoPartialFeedbackRecord()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingDailyFeedbackSaveInterceptor());
        var interceptor = host.GetRequiredService<FailingDailyFeedbackSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var notifications = host.GetRequiredService<RecordingNotificationService>();

        interceptor.FailOnNextFeedbackSave = true;

        var act = async () => await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DailyPatientFeedback save failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DailyPatientFeedbacks.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        notifications.Notifications.Should().BeEmpty();
    }

    // 20. Notification side effects.
    [Fact]
    public async Task SubmitFeedback_NotifiesDoctorOnCreateAndUpdate()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var request = ExerciseManagementTestHelpers.ValidFeedbackRequest(doctorId: scenario.Doctor.Id);

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(controller, request);
        notifications.Notifications.Should().ContainSingle(n =>
            n.UserId == scenario.Doctor.Id
            && n.Type == NotificationType.DailyFeedbackReceived
            && n.Title == "New daily feedback");

        notifications.Notifications.Clear();

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(controller, request);
        notifications.Notifications.Should().ContainSingle(n =>
            n.UserId == scenario.Doctor.Id
            && n.Type == NotificationType.DailyFeedbackReceived
            && n.Title == "Daily feedback updated");
    }

    [Fact]
    public async Task GetPatientExerciseStats_AfterFeedbackSubmission_ReflectsScoresInSummary()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today);

        var patientController = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            patientController,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 5,
                hardnessScore: 2));

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.GetPatientExerciseStats(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            from: today,
            to: today,
            CancellationToken.None);

        var stats = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientExerciseStatsResponse>().Subject;
        stats.Summary.FeedbackDayCount.Should().Be(1);
        stats.Summary.AverageImprovementScore.Should().Be(5);
        stats.Summary.AverageHardnessScore.Should().Be(2);
        stats.Daily.Should().ContainSingle(d =>
            d.Date == today
            && d.ImprovementScore == 5
            && d.HardnessScore == 2);
    }

    [Fact]
    public async Task SubmitFeedback_WhenCommentIsWhitespaceOnly_StoresNull()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);

        await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                comment: "   "));

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DailyPatientFeedbacks.SingleAsync()).Comment.Should().BeNull();
    }

    [Fact]
    public async Task SubmitFeedback_WhenSoftDeletedFeedbackExists_ReEnablesAndUpdates()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var existing = await ExerciseManagementTestHostSeeder.SeedFeedbackAsync(
            host,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            isEnabled: false);

        var controller = host.CreatePatientDailyFeedbackController(scenario.Patient.Id);
        var result = await ExerciseManagementTestHelpers.SubmitFeedbackWithValidationAsync(
            controller,
            ExerciseManagementTestHelpers.ValidFeedbackRequest(
                doctorId: scenario.Doctor.Id,
                improvementScore: 4,
                comment: "Restored"));

        result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SubmitDailyFeedbackResponse>().Subject
            .DailyPatientFeedbackId.Should().Be(existing.DailyPatientFeedbackId);

        host.DbContext.ChangeTracker.Clear();
        var stored = await host.DbContext.DailyPatientFeedbacks.SingleAsync();
        stored.IsEnabled.Should().BeTrue();
        stored.Comment.Should().Be("Restored");
    }
}
