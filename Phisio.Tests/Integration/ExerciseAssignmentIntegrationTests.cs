using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers;
using Phisio.Api.Controllers.Admin;
using Phisio.Api.Controllers.Doctor;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Admin.Assignments;
using Phisio.Application.Assignments;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class ExerciseAssignmentIntegrationTests
{
    // 8. Doctor assigns an exercise to their patient.
    [Fact]
    public async Task CreateAssignment_WhenDoctorAndPatientAreLinked_ReturnsCreatedAndPersists()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<AssignmentDto>().Subject;
        body.DoctorId.Should().Be(scenario.Doctor.Id);
        body.PatientId.Should().Be(scenario.Patient.Id);
        body.ExerciseId.Should().Be(scenario.AdminExercise.ExerciseId);
        body.IsActive.Should().BeTrue();

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.UserExercises.SingleAsync();
        persisted.DoctorId.Should().Be(scenario.Doctor.Id);
        persisted.PatientId.Should().Be(scenario.Patient.Id);
        persisted.ExerciseId.Should().Be(scenario.AdminExercise.ExerciseId);
        persisted.IsActive.Should().BeTrue();
        persisted.ScheduledDate.Should().Be(ExerciseManagementTestHelpers.Today);
    }

    // 9. Doctor cannot assign an exercise to a patient who is not connected to them.
    [Fact]
    public async Task CreateAssignment_WhenPatientIsNotLinkedToDoctor_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.OtherPatient.Id,
            scenario.AdminExercise.ExerciseId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 10. Doctor cannot assign through a Clinic where the Patient–Doctor relationship does not exist.
    [Fact]
    public async Task CreateAssignment_WhenOnlyPendingRelationshipExists_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);

        host.DbContext.DoctorPatients.RemoveRange(host.DbContext.DoctorPatients);
        await host.DbContext.SaveChangesAsync();
        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            DoctorPatientStatus.Pending);

        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 11. Invalid PatientId is rejected.
    [Fact]
    public async Task CreateAssignment_WhenPatientIdDoesNotExist_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            Guid.NewGuid(),
            scenario.AdminExercise.ExerciseId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 12. Invalid ExerciseId is rejected.
    [Fact]
    public async Task CreateAssignment_WhenExerciseIdDoesNotExist_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            Guid.NewGuid());

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 13. Duplicate exercise assignment is prevented.
    // 26. Duplicate assignments must not create duplicate database records.
    [Fact]
    public async Task CreateAssignment_WhenDuplicateActiveAssignmentExists_MergesInPlaceWithoutSecondRow()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        await controller.CreateAssignment(request, CancellationToken.None);
        var originalAssignedAt = (await host.DbContext.UserExercises.SingleAsync()).AssignedAt;

        await Task.Delay(10);
        var secondResult = await controller.CreateAssignment(request, CancellationToken.None);

        secondResult.Should().BeOfType<CreatedAtActionResult>();
        host.DbContext.ChangeTracker.Clear();
        var rows = await host.DbContext.UserExercises.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].AssignedAt.Should().BeAfter(originalAssignedAt);
    }

    // 14. Doctor removes/unassigns an exercise from a patient.
    [Fact]
    public async Task DeactivateAssignment_WhenDoctorOwnsAssignment_ReturnsNoContentAndDeactivates()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var assignController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        var assignResult = await assignController.CreateAssignment(request, CancellationToken.None);
        var assignmentId = assignResult.Should().BeOfType<CreatedAtActionResult>().Subject
            .Value.Should().BeOfType<AssignmentDto>().Subject.Id;

        var deactivateResult = await assignController.DeactivateAssignment(
            assignmentId,
            CancellationToken.None);

        deactivateResult.Should().BeOfType<NoContentResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.UserExercises.SingleAsync()).IsActive.Should().BeFalse();
    }

    // 15. Patient can see their assigned exercises.
    [Fact]
    public async Task GetMyAssignments_WhenPatientHasActiveAssignments_ReturnsAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var patientAssignmentsController = host.CreateAssignmentsController(
            scenario.Patient.Id,
            RoleNames.Patient);
        var result = await patientAssignmentsController.GetMyAssignments(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var assignments = ok.Value.Should().BeAssignableTo<IReadOnlyList<AssignmentDto>>().Subject;
        assignments.Should().ContainSingle()
            .Which.ExerciseId.Should().Be(scenario.AdminExercise.ExerciseId);
    }

    [Fact]
    public async Task GetExercises_WhenPatientHasAssignedExercises_ReturnsPatientView()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);
        var result = await patientController.GetExercises(
            scheduledDate: ExerciseManagementTestHelpers.Today,
            doctorId: null,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        body.Should().NotBeNull();
    }

    // 16. Patient cannot see another patient's exercises.
    [Fact]
    public async Task GetMyAssignments_WhenPatientRequestsOwnList_DoesNotIncludeOtherPatientsAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);

        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);
        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.OtherPatient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var patientController = host.CreateAssignmentsController(
            scenario.Patient.Id,
            RoleNames.Patient);
        var result = await patientController.GetMyAssignments(CancellationToken.None);

        var assignments = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<AssignmentDto>>().Subject;
        assignments.Should().ContainSingle();
        assignments[0].PatientId.Should().Be(scenario.Patient.Id);
    }

    // 17. Doctor can see exercises assigned to their patients.
    [Fact]
    public async Task GetPatientAssignments_WhenDoctorIsLinked_ReturnsAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var result = await doctorController.GetPatientAssignments(
            scenario.Patient.Id,
            CancellationToken.None);

        var assignments = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<AssignmentDto>>().Subject;
        assignments.Should().ContainSingle()
            .Which.ExerciseTitle.Should().Be(scenario.AdminExercise.Title);
    }

    // 18. Doctor cannot access another doctor's patient assignments.
    [Fact]
    public async Task GetPatientAssignments_WhenDoctorIsNotLinkedToPatient_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var primaryDoctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await primaryDoctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var otherDoctorController = host.CreateAssignmentsController(
            scenario.OtherDoctor.Id,
            RoleNames.Doctor);
        var result = await otherDoctorController.GetPatientAssignments(
            scenario.Patient.Id,
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // 19. Admin can manage exercise assignments according to existing authorization rules.
    [Fact]
    public async Task AdminAssignmentsController_AllowsReportAccessForAdminOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var doctor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Doctor)],
            authenticationType: "Test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeTrue();

        typeof(AdminAssignmentsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.AdminOnly);
    }

    [Fact]
    public async Task AdminAssignmentsController_ReturnsReportForExistingAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await doctorController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        var adminController = host.CreateAdminAssignmentsController(
            scenario.Admin.Id,
            RoleNames.Admin);
        var result = await adminController.GetReport(CancellationToken.None);

        var report = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<AssignmentReportDto>>().Subject;
        report.Should().ContainSingle();
        report[0].PatientName.Should().Be(scenario.Patient.Name);
        report[0].DoctorName.Should().Be(scenario.Doctor.Name);
        report[0].ExerciseNames.Should().Contain(scenario.AdminExercise.Title);
    }

    [Fact]
    public async Task CreateAssignment_WhenCallerIsAdminRole_StillRequiresDoctorOnlyPolicyAtRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.Doctor));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();

        typeof(AssignmentsController)
            .GetMethod(nameof(AssignmentsController.CreateAssignment))!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.DoctorOnly);
    }

    // 20. Unauthorized/anonymous users cannot access protected exercise endpoints.
    [Fact]
    public async Task Authorization_ProtectedExerciseEndpoints_RejectAnonymousAndCrossRoleAccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.Doctor));
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var patient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Patient)],
            authenticationType: "Test"));
        var doctor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Doctor)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AssignmentsController_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreateAssignmentsController(userId: null, RoleNames.Doctor);

        var result = await controller.CreateAssignment(
            new CreateAssignmentRequest
            {
                PatientId = Guid.NewGuid(),
                ExerciseId = Guid.NewGuid(),
            },
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PatientExercisesController_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreatePatientExercisesController(userId: null);

        var result = await controller.GetExercises(cancellationToken: CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // 21. Exercise assignment respects the existing Patient–Doctor–Clinic relationship.
    [Fact]
    public async Task CreateAssignment_RequiresApprovedDoctorPatientRelationshipRegardlessOfClinic()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host, includeSecondClinic: true);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(1);
    }

    // 22. Multi-clinic relationship: assignment is not scoped by ClinicId.
    [Fact]
    public async Task CreateAssignment_WhenApprovedLinksExistInMultipleClinics_CreatesSingleAssignmentRow()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host, includeSecondClinic: true);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId);

        await controller.CreateAssignment(request, CancellationToken.None);

        host.DbContext.ChangeTracker.Clear();
        var links = await host.DbContext.DoctorPatients
            .Where(link => link.DoctorId == scenario.Doctor.Id && link.PatientId == scenario.Patient.Id)
            .ToListAsync();
        links.Should().HaveCount(2);
        links.Select(link => link.ClinicId).Should().BeEquivalentTo([scenario.ClinicAId, scenario.ClinicBId]);

        var assignments = await host.DbContext.UserExercises.ToListAsync();
        assignments.Should().ContainSingle();
        assignments[0].DoctorId.Should().Be(scenario.Doctor.Id);
        assignments[0].PatientId.Should().Be(scenario.Patient.Id);
    }

    // 23. Invalid/non-existing ClinicId is rejected where ClinicId is required.
    [Fact]
    public void AssignmentEndpoints_DoNotAcceptClinicId_CurrentBehaviorIsDoctorPatientScoped()
    {
        typeof(CreateAssignmentRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["PatientId", "ExerciseId"]);

        typeof(AssignPatientExercisesRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["Items", "ScheduledDates"]);
    }

    // 24. Verify DoctorId, PatientId, ExerciseId persisted; UserExercise has no ClinicId.
    [Fact]
    public async Task CreateAssignment_PersistsDoctorPatientExerciseIdsWithoutClinicId()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await controller.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        host.DbContext.ChangeTracker.Clear();
        var assignment = await host.DbContext.UserExercises.SingleAsync();
        assignment.DoctorId.Should().Be(scenario.Doctor.Id);
        assignment.PatientId.Should().Be(scenario.Patient.Id);
        assignment.ExerciseId.Should().Be(scenario.AdminExercise.ExerciseId);

        typeof(UserExercise)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("ClinicId");
    }

    // 25. Verify foreign keys and unique constraints.
    [Fact]
    public async Task UserExerciseModel_DefinesExpectedForeignKeysAndUniqueIndex()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var entityType = host.DbContext.Model.FindEntityType(typeof(UserExercise));
        entityType.Should().NotBeNull();

        var foreignKeys = entityType!.GetForeignKeys()
            .Select(fk => string.Join(',', fk.Properties.Select(property => property.Name)))
            .ToList();
        foreignKeys.Should().Contain("PatientId");
        foreignKeys.Should().Contain("DoctorId");
        foreignKeys.Should().Contain("ExerciseId");

        var expectedUniqueColumns = new[] { "DoctorId", "ExerciseId", "PatientId", "ScheduledDate" };
        var uniqueIndex = entityType.GetIndexes()
            .Single(index =>
                index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .OrderBy(name => name)
                    .SequenceEqual(expectedUniqueColumns.OrderBy(name => name)));
        uniqueIndex.IsUnique.Should().BeTrue();
        uniqueIndex.Properties.Select(property => property.Name)
            .Should().BeEquivalentTo(["PatientId", "DoctorId", "ExerciseId", "ScheduledDate"]);
    }

    // DoctorPatientsController path: doctor-owned exercises only.
    [Fact]
    public async Task AssignPatientExercises_WhenExerciseIsDoctorOwned_ReturnsOkAndPersists()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = new AssignPatientExercisesRequest(
            [new AssignPatientExerciseItem(scenario.DoctorExercise.ExerciseId, Sets: 3, Reps: "10", null, null)],
            [ExerciseManagementTestHelpers.Today]);

        var result = await controller.AssignPatientExercises(
            scenario.Patient.Id,
            request,
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        host.DbContext.ChangeTracker.Clear();
        var assignment = await host.DbContext.UserExercises.SingleAsync();
        assignment.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);
        assignment.Sets.Should().Be(3);
        assignment.Reps.Should().Be("10");
    }

    [Fact]
    public async Task AssignPatientExercises_WhenExerciseIsAdminCatalogOnly_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = new AssignPatientExercisesRequest(
            [new AssignPatientExerciseItem(scenario.AdminExercise.ExerciseId, null, null, null, null)],
            [ExerciseManagementTestHelpers.Today]);

        var result = await controller.AssignPatientExercises(
            scenario.Patient.Id,
            request,
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 27. Simulate save failure during assignment and verify no partial assignment remains.
    [Fact]
    public async Task CreateAssignment_WhenSaveFails_LeavesNoPartialAssignment()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingUserExerciseSaveInterceptor());

        var interceptor = host.GetRequiredService<FailingUserExerciseSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        interceptor.FailOnNextUserExerciseSave = true;

        var act = async () => await controller.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated UserExercise save failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }
}
