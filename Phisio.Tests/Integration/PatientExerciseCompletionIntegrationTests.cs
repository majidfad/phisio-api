using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end coverage for patient today-schedule and exercise completion.
///
/// Architecture notes (current implementation):
/// - Patient endpoints: GET /api/patient/exercises/today, GET /api/patient/exercises, POST /api/patient/exercises/complete.
/// - Today list requires an approved DoctorPatient link and ScheduledDate == today.
/// - Completion does NOT require ScheduledDate == today; CompletionDate is always UTC today.
/// - Duplicate completion for the same UserExercise+today is skipped (idempotent).
/// - Completing notifies the doctor via INotificationService (ExercisesCompleted).
/// - There is no patient completion-history endpoint; doctor history/stats cover adherence.
/// - UserExercise has Sets/Reps/PatientCue; there is no Duration field.
/// </summary>
public sealed class PatientExerciseCompletionIntegrationTests
{
    // 1. Get today's exercises.
    [Fact]
    public async Task GetTodayExercises_ReturnsOnlyActiveExercisesScheduledForToday()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var today = ExerciseManagementTestHelpers.Today;

        var todayAssignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today,
            sets: 4,
            reps: "12",
            patientCue: "Slow");
        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise2.ExerciseId,
            scheduledDate: today.AddDays(1));
        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId,
            scheduledDate: today.AddDays(-1));
        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today,
            isActive: false);

        // Distinct exercise for retired row to avoid unique index conflict with active today row.
        var retiredExercise = ExerciseBuilder.Create(
            title: "Retired Stretch",
            createdByDoctorId: scenario.Doctor.Id);
        host.DbContext.Exercises.Add(retiredExercise);
        await host.DbContext.SaveChangesAsync();
        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            retiredExercise.ExerciseId,
            scheduledDate: today,
            isEnabled: false);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.GetTodayExercises(
            doctorId: null,
            clinicId: null,
            cancellationToken: CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject;
        body.DoctorGroups.Should().ContainSingle();
        body.DoctorGroups[0].DoctorId.Should().Be(scenario.Doctor.Id);
        body.DoctorGroups[0].Exercises.Should().ContainSingle();

        var item = body.DoctorGroups[0].Exercises[0];
        item.UserExerciseId.Should().Be(todayAssignment.UserExerciseId);
        item.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);
        item.Title.Should().Be(scenario.DoctorExercise.Title);
        item.ScheduledDate.Should().Be(today);
        item.CompletedToday.Should().BeFalse();
        item.Sets.Should().Be(4);
        item.Reps.Should().Be("12");
        item.PatientCue.Should().Be("Slow");
        item.VideoUrl.Should().Be(scenario.DoctorExercise.VideoUrl);
        item.MediaType.Should().Be(scenario.DoctorExercise.MediaType);
    }

    // 2. Patient isolation.
    [Fact]
    public async Task GetTodayExercises_DoesNotReturnOtherPatientsExercises()
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

        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);
        await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.DoctorExercise2.ExerciseId,
            scheduledDate: today);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.GetTodayExercises(cancellationToken: CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject;
        body.DoctorGroups.Should().ContainSingle();
        body.DoctorGroups[0].Exercises.Should().ContainSingle()
            .Which.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);
    }

    [Fact]
    public async Task GetTodayExercises_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreatePatientExercisesController(userId: null, RoleNames.Patient);

        var result = await controller.GetTodayExercises(cancellationToken: CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // 3. Complete an exercise.
    [Fact]
    public async Task CompleteExercises_WhenAssignmentIsActive_PersistsCompletion()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CompleteExercisesResponse>().Subject;
        body.CompletionDate.Should().Be(ExerciseManagementTestHelpers.Today);
        body.CreatedUserExerciseIds.Should().ContainSingle().Which.Should().Be(assignment.UserExerciseId);
        body.SkippedUserExerciseIds.Should().BeEmpty();

        host.DbContext.ChangeTracker.Clear();
        var completion = await host.DbContext.ExerciseCompletions.SingleAsync();
        completion.UserExerciseId.Should().Be(assignment.UserExerciseId);
        completion.PatientId.Should().Be(scenario.Patient.Id);
        completion.DoctorId.Should().Be(scenario.Doctor.Id);
        completion.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);
        completion.CompletionDate.Should().Be(ExerciseManagementTestHelpers.Today);
        completion.IsEnabled.Should().BeTrue();
        completion.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    // 4. Existing business rules for past/future scheduled assignments.
    [Fact]
    public async Task CompleteExercises_WhenAssignmentIsScheduledForFuture_StillCompletesForToday_UnderCurrentRules()
    {
        // Current behavior: CompleteExercisesAsync does not require ScheduledDate == today.
        // CompletionDate is always UTC today against the given UserExerciseId.
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var future = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: ExerciseManagementTestHelpers.Today.AddDays(2));

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [future.UserExerciseId] },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CompleteExercisesResponse>()
            .Which.CreatedUserExerciseIds.Should().ContainSingle();

        host.DbContext.ChangeTracker.Clear();
        var completion = await host.DbContext.ExerciseCompletions.SingleAsync();
        completion.UserExerciseId.Should().Be(future.UserExerciseId);
        completion.CompletionDate.Should().Be(ExerciseManagementTestHelpers.Today);

        // Today schedule still excludes the future-scheduled assignment.
        var todayResult = await controller.GetTodayExercises(cancellationToken: CancellationToken.None);
        todayResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject
            .DoctorGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteExercises_WhenAssignmentIsRetired_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var retired = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            isActive: false);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [retired.UserExerciseId] },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await host.DbContext.ExerciseCompletions.CountAsync()).Should().Be(0);
    }

    // 5. Duplicate completion — idempotent skip.
    [Fact]
    public async Task CompleteExercises_WhenAlreadyCompletedToday_SkipsWithoutDuplicateRow()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        var second = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        var body = second.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CompleteExercisesResponse>().Subject;
        body.CreatedUserExerciseIds.Should().BeEmpty();
        body.SkippedUserExerciseIds.Should().ContainSingle().Which.Should().Be(assignment.UserExerciseId);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExerciseCompletions.CountAsync()).Should().Be(1);
    }

    // 6. Completion ownership.
    [Fact]
    public async Task CompleteExercises_WhenAssignmentBelongsToAnotherPatient_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);
        var otherAssignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.DoctorExercise.ExerciseId);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [otherAssignment.UserExerciseId] },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await host.DbContext.ExerciseCompletions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteExercises_PatientOnlyPolicy_RejectsDoctorAndAdminRoles()
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
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));
        var patient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Patient)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeTrue();

        typeof(PatientExercisesController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.PatientOnly);
    }

    // 7. Completion status reflected on today list.
    [Fact]
    public async Task GetTodayExercises_AfterCompletion_MarksCompletedTodayTrue()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        var result = await controller.GetTodayExercises(cancellationToken: CancellationToken.None);
        var item = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject
            .DoctorGroups.Should().ContainSingle().Subject
            .Exercises.Should().ContainSingle().Subject;

        item.CompletedToday.Should().BeTrue();
        item.UserExerciseId.Should().Be(assignment.UserExerciseId);
    }

    // 8. Completion history (doctor endpoint — no patient history API).
    [Fact]
    public async Task GetPatientExerciseHistory_AfterCompletion_IncludesCompletedExerciseForToday()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);
        await patientController.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.GetPatientExerciseHistory(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        var history = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientExerciseHistoryResponse>().Subject;
        history.Patient.PatientId.Should().Be(scenario.Patient.Id);
        history.Summary.AssignedExerciseCount.Should().BeGreaterThan(0);
        history.DailyHistory.Should().Contain(day =>
            day.Date == ExerciseManagementTestHelpers.Today
            && day.Exercises.Any(ex =>
                ex.UserExerciseId == assignment.UserExerciseId && ex.IsCompleted));
    }

    // 9. Completion statistics.
    [Fact]
    public async Task GetPatientExerciseStats_AfterCompletion_MatchesDatabaseCompletions()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var today = ExerciseManagementTestHelpers.Today;
        var first = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId,
            scheduledDate: today);
        var second = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise2.ExerciseId,
            scheduledDate: today);

        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);
        await patientController.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [first.UserExerciseId] },
            CancellationToken.None);

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.GetPatientExerciseStats(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            from: today,
            to: today,
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        host.DbContext.ChangeTracker.Clear();
        var completionCount = await host.DbContext.ExerciseCompletions.CountAsync(c =>
            c.PatientId == scenario.Patient.Id
            && c.DoctorId == scenario.Doctor.Id
            && c.IsEnabled
            && c.CompletionDate == today);
        completionCount.Should().Be(1);

        var assignmentCount = await host.DbContext.UserExercises.CountAsync(ue =>
            ue.PatientId == scenario.Patient.Id
            && ue.DoctorId == scenario.Doctor.Id
            && ue.ScheduledDate == today
            && ue.IsActive
            && ue.IsEnabled);
        assignmentCount.Should().Be(2);
        // second remains incomplete
        (await host.DbContext.ExerciseCompletions.AnyAsync(c =>
                c.UserExerciseId == second.UserExerciseId))
            .Should().BeFalse();
    }

    // 10. Notifications / side effects.
    [Fact]
    public async Task CompleteExercises_NotifiesDoctorWithExercisesCompleted()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);

        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);
        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        notifications.Notifications.Should().ContainSingle(n =>
            n.UserId == scenario.Doctor.Id
            && n.Type == NotificationType.ExercisesCompleted
            && n.Title == "Exercises completed");
    }

    [Fact]
    public async Task CompleteExercises_WhenDuplicateSkipped_DoesNotNotifyAgain()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);
        notifications.Notifications.Clear();

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        notifications.Notifications.Should().BeEmpty();
    }

    // 11. Authorization.
    [Fact]
    public async Task Authorization_AnonymousRejectedForPatientOnlyPolicy()
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

    [Fact]
    public async Task CompleteExercises_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreatePatientExercisesController(userId: null, RoleNames.Patient);

        var result = await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [Guid.NewGuid()] },
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // 12. Data integrity.
    [Fact]
    public async Task CompleteExercises_PersistsForeignKeysAndUniqueIndexShape()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        host.DbContext.ChangeTracker.Clear();
        var completion = await host.DbContext.ExerciseCompletions.SingleAsync();
        completion.UserExerciseId.Should().Be(assignment.UserExerciseId);
        completion.PatientId.Should().Be(scenario.Patient.Id);
        completion.DoctorId.Should().Be(scenario.Doctor.Id);
        completion.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);

        var entityType = host.DbContext.Model.FindEntityType(typeof(ExerciseCompletion));
        entityType.Should().NotBeNull();
        entityType!.GetForeignKeys()
            .SelectMany(fk => fk.Properties.Select(p => p.Name))
            .Should().Contain("UserExerciseId");

        var expectedUnique = new[] { "UserExerciseId", "CompletionDate" };
        entityType.GetIndexes()
            .Where(index => index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).OrderBy(n => n).ToArray())
            .Should().ContainEquivalentOf(expectedUnique.OrderBy(n => n).ToArray());
    }

    // 13. Save failure / rollback.
    [Fact]
    public async Task CompleteExercises_WhenSaveFails_LeavesNoPartialCompletion()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingExerciseCompletionSaveInterceptor());
        var interceptor = host.GetRequiredService<FailingExerciseCompletionSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignment = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var notifications = host.GetRequiredService<RecordingNotificationService>();
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        interceptor.FailOnNextCompletionSave = true;

        var act = async () => await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [assignment.UserExerciseId] },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated ExerciseCompletion save failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExerciseCompletions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        notifications.Notifications.Should().BeEmpty();
    }

    // 14. Multiple exercises today.
    [Fact]
    public async Task CompleteExercises_WhenMultipleToday_CompletingOneDoesNotAffectOthers()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var first = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise.ExerciseId);
        var second = await ExerciseManagementTestHostSeeder.SeedAssignmentAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.DoctorExercise2.ExerciseId);
        var controller = host.CreatePatientExercisesController(scenario.Patient.Id);

        await controller.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [first.UserExerciseId] },
            CancellationToken.None);

        var today = await controller.GetTodayExercises(cancellationToken: CancellationToken.None);
        var items = today.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject
            .DoctorGroups.Should().ContainSingle().Subject
            .Exercises;

        items.Should().HaveCount(2);
        items.Single(i => i.UserExerciseId == first.UserExerciseId).CompletedToday.Should().BeTrue();
        items.Single(i => i.UserExerciseId == second.UserExerciseId).CompletedToday.Should().BeFalse();

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExerciseCompletions.CountAsync()).Should().Be(1);
    }

    // 15. Program-generated exercises → today → completion.
    [Fact]
    public async Task ProgramMaterializedAssignments_AppearInTodayAndCanBeCompleted()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            doctorController,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(
                    scenario.DoctorExercise.ExerciseId,
                    sets: 3,
                    reps: "10",
                    patientCue: "From program")));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        host.DbContext.ChangeTracker.Clear();
        var todayAssignment = await host.DbContext.UserExercises.SingleAsync(ue =>
            ue.ProgramId == programId && ue.ScheduledDate == today);

        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);
        var todayResult = await patientController.GetTodayExercises(cancellationToken: CancellationToken.None);
        var item = todayResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject
            .DoctorGroups.Should().ContainSingle().Subject
            .Exercises.Should().ContainSingle().Subject;

        item.UserExerciseId.Should().Be(todayAssignment.UserExerciseId);
        item.Sets.Should().Be(3);
        item.Reps.Should().Be("10");
        item.PatientCue.Should().Be("From program");
        item.CompletedToday.Should().BeFalse();

        var completeResult = await patientController.CompleteExercises(
            new CompleteExercisesRequest { UserExerciseIds = [todayAssignment.UserExerciseId] },
            CancellationToken.None);
        completeResult.Should().BeOfType<OkObjectResult>();

        var after = await patientController.GetTodayExercises(cancellationToken: CancellationToken.None);
        after.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject
            .DoctorGroups[0].Exercises[0].CompletedToday.Should().BeTrue();

        host.DbContext.ChangeTracker.Clear();
        var completion = await host.DbContext.ExerciseCompletions.SingleAsync();
        completion.UserExerciseId.Should().Be(todayAssignment.UserExerciseId);
        completion.PatientId.Should().Be(scenario.Patient.Id);

        var linkedAssignment = await host.DbContext.UserExercises.SingleAsync(ue =>
            ue.UserExerciseId == todayAssignment.UserExerciseId);
        linkedAssignment.ProgramId.Should().Be(programId);
    }
}
