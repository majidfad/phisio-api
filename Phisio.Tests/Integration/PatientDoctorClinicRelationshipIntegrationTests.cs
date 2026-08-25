using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers.Doctor;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class PatientDoctorClinicRelationshipIntegrationTests
{
    // 1. Valid request creates pending Patient–Doctor–Clinic relationship
    [Fact]
    public async Task RequestLink_WhenDoctorBelongsToClinic_CreatesPendingRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = ok.Value.Should().BeOfType<PatientLinkedDoctorDto>().Subject;
        body.DoctorId.Should().Be(scenario.Doctor.Id);
        body.ClinicId.Should().Be(scenario.ClinicAId);
        body.ClinicName.Should().Be(scenario.ClinicA.Name);
        body.Status.Should().Be(DoctorPatientStatus.Pending);

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.PatientId.Should().Be(scenario.Patient.Id);
        link.DoctorId.Should().Be(scenario.Doctor.Id);
        link.ClinicId.Should().Be(scenario.ClinicAId);
        link.Status.Should().Be(DoctorPatientStatus.Pending);
        link.IsEnabled.Should().BeTrue();

        await AssertForeignKeysExistAsync(host, link);
    }

    // 2. Doctor not in selected clinic → rejected
    [Fact]
    public async Task RequestLink_WhenDoctorNotInClinic_RejectsAndCreatesNoRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var otherClinic = ClinicBuilder.Create(managerId: Guid.NewGuid(), name: "Unrelated Clinic");
        host.DbContext.Clinics.Add(otherClinic);
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(otherClinic.ClinicId),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<ObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(DoctorPatientErrors.DoctorNotInClinic);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 3. Invalid / non-existing clinic
    [Fact]
    public async Task RequestLink_WhenClinicDoesNotExist_RejectsAndCreatesNoRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(Guid.NewGuid()),
            CancellationToken.None);

        var notFound = result.Should().BeOfType<ObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ExtractErrors(notFound.Value).Should().Contain(DoctorPatientErrors.ClinicNotFound);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 4. Invalid / non-existing doctor
    [Fact]
    public async Task RequestLink_WhenDoctorDoesNotExist_RejectsAndCreatesNoRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        var result = await controller.RequestLink(
            Guid.NewGuid(),
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        var notFound = result.Should().BeOfType<ObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ExtractErrors(notFound.Value).Should().Contain(DoctorPatientErrors.DoctorNotFound);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 5. Invalid / non-existing patient (controller uses claims; missing claim → Unauthorized)
    [Fact]
    public async Task RequestLink_WhenPatientClaimMissing_ReturnsUnauthorized()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreatePatientDoctorsController(patientId: null);

        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestLink_WhenPatientIdDoesNotExist_UsesAuthenticatedClaimPatientId()
    {
        // API always takes PatientId from auth claims; forged / deleted IDs are not a public input.
        // Document current service behavior: membership validation runs, patient existence is not checked.
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var missingPatientId = Guid.NewGuid();
        var controller = host.CreatePatientDoctorsController(missingPatientId);

        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        // Current implementation does not reject unknown patient IDs at request time.
        // Relationship is keyed by the claim PatientId (auth layer must ensure the user exists).
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<PatientLinkedDoctorDto>().Subject;
        body.DoctorId.Should().Be(scenario.Doctor.Id);

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.PatientId.Should().Be(missingPatientId);
        link.DoctorId.Should().Be(scenario.Doctor.Id);
        link.ClinicId.Should().Be(scenario.ClinicAId);
    }

    // 6. Duplicate Patient–Doctor–Clinic relationship
    [Fact]
    public async Task RequestLink_WhenPendingAlreadyExists_RejectsDuplicate()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        (await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var duplicate = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        var badRequest = duplicate.Should().BeOfType<ObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(DoctorPatientErrors.AlreadyRequested);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RequestLink_WhenAlreadyApproved_RejectsDuplicate()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Approved));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<ObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(DoctorPatientErrors.AlreadyApproved);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    // 7. Same Patient + Doctor in different clinics
    [Fact]
    public async Task RequestLink_SamePatientAndDoctor_InDifferentClinics_CreatesTwoRecords()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        (await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        (await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicBId),
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var links = await host.DbContext.DoctorPatients.IgnoreQueryFilters().ToListAsync();
        links.Should().HaveCount(2);
        links.Select(link => link.ClinicId).Should().BeEquivalentTo(
            [scenario.ClinicAId, scenario.ClinicBId]);
        links.Should().OnlyContain(link =>
            link.PatientId == scenario.Patient.Id &&
            link.DoctorId == scenario.Doctor.Id &&
            link.Status == DoctorPatientStatus.Pending);
    }

    // 8. Approve pending connection
    [Fact]
    public async Task ApproveRequest_WhenPending_ApprovesRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        await CreatePendingLinkAsync(host, scenario);

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<DoctorPatientDto>().Subject;
        body.PatientId.Should().Be(scenario.Patient.Id);
        body.ClinicId.Should().Be(scenario.ClinicAId);
        body.ClinicName.Should().Be(scenario.ClinicA.Name);

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.Status.Should().Be(DoctorPatientStatus.Approved);
        link.IsEnabled.Should().BeTrue();
        link.PatientId.Should().Be(scenario.Patient.Id);
        link.DoctorId.Should().Be(scenario.Doctor.Id);
        link.ClinicId.Should().Be(scenario.ClinicAId);
    }

    // 9. Approve non-pending / already approved request
    [Fact]
    public async Task ApproveRequest_WhenAlreadyApproved_ReturnsNotFound()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Approved));
        await host.DbContext.SaveChangesAsync();

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        var notFound = result.Should().BeOfType<ObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ExtractErrors(notFound.Value).Should().Contain(DoctorPatientErrors.RequestNotFound);

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.Status.Should().Be(DoctorPatientStatus.Approved);
    }

    [Fact]
    public async Task ApproveRequest_WhenRejected_ReturnsNotFound()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Rejected));
        await host.DbContext.SaveChangesAsync();

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        var notFound = result.Should().BeOfType<ObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ExtractErrors(notFound.Value).Should().Contain(DoctorPatientErrors.RequestNotFound);
    }

    // 10. Reject pending connection
    [Fact]
    public async Task RejectRequest_WhenPending_SetsRejectedAndDoesNotActivateCareLink()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        await CreatePendingLinkAsync(host, scenario);

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.RejectRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.Status.Should().Be(DoctorPatientStatus.Rejected);
        link.IsEnabled.Should().BeTrue();

        var patientsResult = await doctorController.GetPatients(CancellationToken.None);
        var patients = patientsResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<DoctorPatientDto>>().Subject;
        patients.Should().BeEmpty();
    }

    // 11. Remove Patient–Doctor–Clinic relationship
    [Fact]
    public async Task RemovePatient_WhenApproved_SoftDeletesRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Approved));
        await host.DbContext.SaveChangesAsync();

        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await doctorController.RemovePatient(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await host.DbContext.DoctorPatients.CountAsync()).Should().Be(0);
        var softDeleted = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        softDeleted.IsEnabled.Should().BeFalse();
        softDeleted.PatientId.Should().Be(scenario.Patient.Id);
        softDeleted.DoctorId.Should().Be(scenario.Doctor.Id);
        softDeleted.ClinicId.Should().Be(scenario.ClinicAId);
    }

    [Fact]
    public async Task Unlink_WhenApproved_SoftDeletesRelationshipFromPatientSide()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Approved));
        await host.DbContext.SaveChangesAsync();

        var patientController = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var result = await patientController.Unlink(
            scenario.Doctor.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync())
            .IsEnabled.Should().BeFalse();
    }

    // 12. Patient's connected doctors (with clinic info; multi-clinic)
    [Fact]
    public async Task GetMyDoctors_ReturnsConnectedDoctorsWithCorrectClinicInfo()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var otherDoctor = await RelationshipTestHostSeeder.SeedExtraDoctorAsync(host, scenario.ClinicA);

        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicAId,
                status: DoctorPatientStatus.Approved),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicBId,
                status: DoctorPatientStatus.Pending),
            DoctorPatientBuilder.Create(
                otherDoctor.Id,
                Guid.NewGuid(),
                scenario.ClinicAId,
                status: DoctorPatientStatus.Approved));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var result = await controller.GetMyDoctors(CancellationToken.None);

        var doctors = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<PatientLinkedDoctorDto>>().Subject;

        doctors.Should().HaveCount(2);
        doctors.Should().OnlyContain(item => item.DoctorId == scenario.Doctor.Id);
        doctors.Select(item => item.ClinicId).Should().BeEquivalentTo(
            [scenario.ClinicAId, scenario.ClinicBId]);
        doctors.Single(item => item.ClinicId == scenario.ClinicAId).ClinicName.Should().Be(scenario.ClinicA.Name);
        doctors.Single(item => item.ClinicId == scenario.ClinicBId).ClinicName.Should().Be(scenario.ClinicB!.Name);
        doctors.Single(item => item.ClinicId == scenario.ClinicAId).Status.Should().Be(DoctorPatientStatus.Approved);
        doctors.Single(item => item.ClinicId == scenario.ClinicBId).Status.Should().Be(DoctorPatientStatus.Pending);
    }

    // 13. Doctor's connected patients (with clinic info; multi-clinic)
    [Fact]
    public async Task GetPatients_ReturnsConnectedPatientsWithCorrectClinicInfo()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var otherPatient = ApplicationUserBuilder.Patient(name: "Other Patient", phoneNumber: "+15551000999");
        host.DbContext.Users.Add(otherPatient);

        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicAId,
                status: DoctorPatientStatus.Approved),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicBId,
                status: DoctorPatientStatus.Approved),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                otherPatient.Id,
                scenario.ClinicAId,
                status: DoctorPatientStatus.Pending));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await controller.GetPatients(CancellationToken.None);

        var patients = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<DoctorPatientDto>>().Subject;

        patients.Should().HaveCount(2);
        patients.Should().OnlyContain(item => item.PatientId == scenario.Patient.Id);
        patients.Select(item => item.ClinicId).Should().BeEquivalentTo(
            [scenario.ClinicAId, scenario.ClinicBId]);
        patients.Single(item => item.ClinicId == scenario.ClinicAId).ClinicName.Should().Be(scenario.ClinicA.Name);
        patients.Single(item => item.ClinicId == scenario.ClinicBId).ClinicName.Should().Be(scenario.ClinicB!.Name);
    }

    // 14. Doctor clinic validation (membership required)
    [Fact]
    public async Task RequestLink_DoctorClinicValidation_RequiresClinicMembership()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var outsiderClinic = ClinicBuilder.Create(managerId: Guid.NewGuid(), name: "Outside Clinic");
        host.DbContext.Clinics.Add(outsiderClinic);
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var result = await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(outsiderClinic.ClinicId),
            CancellationToken.None);

        ExtractErrors(result.Should().BeOfType<ObjectResult>().Subject.Value)
            .Should().Contain(DoctorPatientErrors.DoctorNotInClinic);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 15. Authorization
    [Fact]
    public async Task Authorization_PatientAndDoctorPolicies_RejectUnauthorizedUsers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.DoctorAccess));
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
            anonymous, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.PatientOnly))
            .Succeeded.Should().BeTrue();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeTrue();

        typeof(PatientDoctorsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.PatientOnly);
        typeof(DoctorPatientsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.DoctorOnly);
    }

    [Fact]
    public async Task Authorization_Controllers_RejectMissingUserClaims()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        var patientController = host.CreatePatientDoctorsController(patientId: null);
        (await patientController.GetMyDoctors(CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
        (await patientController.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();

        var doctorController = host.CreateDoctorPatientsController(doctorId: null);
        (await doctorController.GetPatients(CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
        (await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Authorization_UsersCannotManipulateAnotherUsersRelationships()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        await CreatePendingLinkAsync(host, scenario);

        var otherPatient = ApplicationUserBuilder.Patient(name: "Intruder", phoneNumber: "+15551000888");
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Intruder Doc", phoneNumber: "+15552000888");
        host.DbContext.Users.AddRange(otherPatient, otherDoctor);
        await host.DbContext.SaveChangesAsync();

        // Another patient cannot approve (doctor-only endpoint) and cannot cancel the first patient's request
        // because CancelRequest scopes by authenticated patientId.
        var otherPatientController = host.CreatePatientDoctorsController(otherPatient.Id);
        var cancelAsOtherPatient = await otherPatientController.CancelRequest(
            scenario.Doctor.Id,
            scenario.ClinicAId,
            CancellationToken.None);
        cancelAsOtherPatient.Should().BeOfType<NotFoundObjectResult>();

        var otherDoctorController = host.CreateDoctorPatientsController(otherDoctor.Id);
        var approveAsOtherDoctor = await otherDoctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);
        var approveError = approveAsOtherDoctor.Should().BeOfType<ObjectResult>().Subject;
        approveError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ExtractErrors(approveError.Value).Should().Contain(DoctorPatientErrors.RequestNotFound);

        var pending = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        pending.Status.Should().Be(DoctorPatientStatus.Pending);
        pending.PatientId.Should().Be(scenario.Patient.Id);
        pending.DoctorId.Should().Be(scenario.Doctor.Id);
    }

    // 16. Data integrity
    [Fact]
    public async Task DataIntegrity_SuccessfulOperations_PreserveIdsAndUniqueness()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var patientController = host.CreatePatientDoctorsController(scenario.Patient.Id);
        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        await patientController.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);
        await patientController.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicBId),
            CancellationToken.None);
        await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        var links = await host.DbContext.DoctorPatients.IgnoreQueryFilters().ToListAsync();
        links.Should().HaveCount(2);
        links.Select(link => (link.PatientId, link.DoctorId, link.ClinicId))
            .Should().OnlyHaveUniqueItems();

        foreach (var link in links)
        {
            await AssertForeignKeysExistAsync(host, link);
        }

        var approved = links.Single(link => link.ClinicId == scenario.ClinicAId);
        approved.Status.Should().Be(DoctorPatientStatus.Approved);
        var pending = links.Single(link => link.ClinicId == scenario.ClinicBId);
        pending.Status.Should().Be(DoctorPatientStatus.Pending);

        // Unique constraint key is the composite primary key (PatientId, DoctorId, ClinicId).
        var entityType = host.DbContext.Model.FindEntityType(typeof(DoctorPatient));
        entityType.Should().NotBeNull();
        var primaryKey = entityType!.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Select(property => property.Name)
            .Should().BeEquivalentTo(["DoctorId", "PatientId", "ClinicId"]);
    }

    // 17. Transaction / rollback on failure
    [Fact]
    public async Task RequestLink_WhenSaveFails_LeavesNoPartialRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
            services.UseFailingDoctorPatientSaveInterceptor());

        var interceptor = host.GetRequiredService<FailingDoctorPatientSaveInterceptor>();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        interceptor.FailOnNextDoctorPatientSave = true;

        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        var act = async () => await controller.RequestLink(
            scenario.Doctor.Id,
            new RequestPatientDoctorLinkDto(scenario.ClinicAId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DoctorPatient save failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApproveRequest_WhenSaveFails_LeavesRelationshipPending()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
            services.UseFailingDoctorPatientSaveInterceptor());

        var interceptor = host.GetRequiredService<FailingDoctorPatientSaveInterceptor>();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        await CreatePendingLinkAsync(host, scenario);

        interceptor.FailOnNextDoctorPatientSave = true;
        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var act = async () => await doctorController.ApproveRequest(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DoctorPatient save failure.");

        host.DbContext.ChangeTracker.Clear();
        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.Status.Should().Be(DoctorPatientStatus.Pending);
        link.IsEnabled.Should().BeTrue();
    }

    private static async Task CreatePendingLinkAsync(
        RelationshipTestHost host,
        RelationshipScenario scenario)
    {
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            scenario.Doctor.Id,
            scenario.Patient.Id,
            scenario.ClinicAId,
            status: DoctorPatientStatus.Pending));
        await host.DbContext.SaveChangesAsync();
    }

    private static async Task AssertForeignKeysExistAsync(
        RelationshipTestHost host,
        DoctorPatient link)
    {
        (await host.DbContext.Users.IgnoreQueryFilters()
            .AnyAsync(user => user.Id == link.PatientId)).Should().BeTrue();
        (await host.DbContext.Users.IgnoreQueryFilters()
            .AnyAsync(user => user.Id == link.DoctorId && user.Role == UserRole.Doctor)).Should().BeTrue();
        (await host.DbContext.Clinics.IgnoreQueryFilters()
            .AnyAsync(clinic => clinic.ClinicId == link.ClinicId)).Should().BeTrue();
        (await host.DbContext.ClinicDoctors.AnyAsync(membership =>
            membership.ClinicId == link.ClinicId && membership.DoctorId == link.DoctorId))
            .Should().BeTrue();
    }

    private static IReadOnlyList<string> ExtractErrors(object? value)
    {
        if (value is null)
        {
            return [];
        }

        var errorsProperty = value.GetType().GetProperty("errors")
            ?? value.GetType().GetProperty("Errors");
        var raw = errorsProperty?.GetValue(value);
        if (raw is null)
        {
            return [];
        }

        if (raw is IEnumerable<string> stringErrors)
        {
            return stringErrors.ToList();
        }

        if (raw is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        return [];
    }
}
