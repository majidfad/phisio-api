using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class ClinicAwarePatientManagementIntegrationTests
{
    [Fact]
    public async Task SearchDoctors_ReturnsClinicListOnDirectoryItems()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var controller = host.CreatePatientDoctorsController(scenario.Patient.Id);

        var result = await controller.SearchDoctors(search: "Ahmadi", specialty: null, CancellationToken.None);

        var doctors = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<PatientDoctorDirectoryItemDto>>().Subject;
        var doctor = doctors.Should().ContainSingle().Subject;
        doctor.DoctorId.Should().Be(scenario.Doctor.Id);
        doctor.Clinics.Select(clinic => clinic.ClinicId).Should().BeEquivalentTo(
            [scenario.ClinicAId, scenario.ClinicBId]);
        doctor.Clinics.Should().OnlyContain(clinic =>
            !string.IsNullOrWhiteSpace(clinic.Name) && clinic.Address.Length >= 0);
    }

    [Fact]
    public async Task AddPatient_WhenDoctorHasSingleClinic_CreatesApprovedRelationship()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var result = await controller.AddPatient(
            new AddDoctorPatientRequest(scenario.Patient.Id, scenario.ClinicAId),
            CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorPatientDto>().Subject;
        body.PatientId.Should().Be(scenario.Patient.Id);
        body.ClinicId.Should().Be(scenario.ClinicAId);
        body.ClinicName.Should().Be(scenario.ClinicA.Name);

        var link = await host.DbContext.DoctorPatients.IgnoreQueryFilters().SingleAsync();
        link.Status.Should().Be(DoctorPatientStatus.Approved);
        link.ClinicId.Should().Be(scenario.ClinicAId);
    }

    [Fact]
    public async Task AddPatient_WhenDoctorNotInClinic_Rejects()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var outsider = ClinicBuilder.Create(managerId: Guid.NewGuid(), name: "Outside Clinic");
        host.DbContext.Clinics.Add(outsider);
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await controller.AddPatient(
            new AddDoctorPatientRequest(scenario.Patient.Id, outsider.ClinicId),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddPatient_SamePatientDoctor_InTwoClinics_CreatesTwoRelationships()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        (await controller.AddPatient(
            new AddDoctorPatientRequest(scenario.Patient.Id, scenario.ClinicAId),
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.AddPatient(
            new AddDoctorPatientRequest(scenario.Patient.Id, scenario.ClinicBId),
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var links = await host.DbContext.DoctorPatients.IgnoreQueryFilters().ToListAsync();
        links.Should().HaveCount(2);
        links.Select(link => link.ClinicId).Should().BeEquivalentTo(
            [scenario.ClinicAId, scenario.ClinicBId]);
        links.Should().OnlyContain(link => link.Status == DoctorPatientStatus.Approved);
    }

    [Fact]
    public async Task LookupPatient_ByPhone_ReturnsExistingPatient()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var result = await controller.LookupPatient(scenario.Patient.PhoneNumber, CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorPatientLookupDto>().Subject;
        body.PatientId.Should().Be(scenario.Patient.Id);
        body.PatientName.Should().Be(scenario.Patient.Name);
    }

    [Fact]
    public async Task GetPatients_WhenFilteredByClinic_ReturnsOnlyThatClinic()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicAId),
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicBId));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var all = await controller.GetPatients(CancellationToken.None);
        all.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<DoctorPatientDto>>()
            .Which.Should().HaveCount(2);

        var filtered = await controller.GetPatients(CancellationToken.None, scenario.ClinicAId);
        var patients = filtered.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<DoctorPatientDto>>().Subject;
        patients.Should().ContainSingle();
        patients.Single().ClinicId.Should().Be(scenario.ClinicAId);
        patients.Single().ClinicName.Should().Be(scenario.ClinicA.Name);
    }

    [Fact]
    public async Task GetPendingRequests_WhenFilteredByClinic_ReturnsOnlyThatClinic()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicAId,
                status: DoctorPatientStatus.Pending),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicBId,
                status: DoctorPatientStatus.Pending));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await controller.GetPendingRequests(CancellationToken.None, scenario.ClinicBId);

        var requests = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<DoctorPatientRequestDto>>().Subject;
        requests.Should().ContainSingle();
        requests.Single().ClinicId.Should().Be(scenario.ClinicBId);
        requests.Single().ClinicName.Should().Be(scenario.ClinicB!.Name);
    }

    [Fact]
    public async Task GetMyClinics_ReturnsMembershipsWithCounts()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicAId),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicBId,
                status: DoctorPatientStatus.Pending));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await controller.GetMyClinics(CancellationToken.None);

        var clinics = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<DoctorClinicOptionDto>>().Subject;
        clinics.Should().HaveCount(2);
        clinics.Single(item => item.ClinicId == scenario.ClinicAId).ActivePatientCount.Should().Be(1);
        clinics.Single(item => item.ClinicId == scenario.ClinicBId).PendingRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboard_WhenFilteredByClinic_ScopesCountsAndClinicInfo()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        var otherPatient = ApplicationUserBuilder.Patient(name: "Other Patient", phoneNumber: "+15551000999");
        host.DbContext.Users.Add(otherPatient);
        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicAId),
            DoctorPatientBuilder.Create(scenario.Doctor.Id, otherPatient.Id, scenario.ClinicBId),
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                otherPatient.Id,
                scenario.ClinicAId,
                status: DoctorPatientStatus.Pending));

        var assignment = new UserExercise
        {
            UserExerciseId = Guid.NewGuid(),
            DoctorId = scenario.Doctor.Id,
            PatientId = scenario.Patient.Id,
            ClinicId = scenario.ClinicAId,
            ExerciseId = Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            IsEnabled = true,
        };
        host.DbContext.UserExercises.Add(assignment);
        host.DbContext.ExerciseCompletions.Add(ExerciseCompletionBuilder.Create(
            assignment.UserExerciseId,
            scenario.Patient.Id,
            scenario.Doctor.Id,
            assignment.ExerciseId));
        host.DbContext.DailyPatientFeedbacks.Add(DailyPatientFeedbackBuilder.Create(
            scenario.Patient.Id,
            scenario.Doctor.Id));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorDashboardController(scenario.Doctor.Id);

        var all = (await controller.GetDashboard(CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorDashboardDto>().Subject;
        all.PatientsCount.Should().Be(2);
        all.PendingRequestsCount.Should().Be(1);
        all.AssignedExercisesCount.Should().Be(1);
        all.CompletedExercisesCount.Should().Be(1);
        all.FeedbackCount.Should().Be(1);
        all.RecentPatients.Should().OnlyContain(item =>
            item.ClinicId != Guid.Empty && !string.IsNullOrWhiteSpace(item.ClinicName));

        var clinicA = (await controller.GetDashboard(CancellationToken.None, scenario.ClinicAId))
            .Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorDashboardDto>().Subject;
        clinicA.PatientsCount.Should().Be(1);
        clinicA.PendingRequestsCount.Should().Be(1);
        clinicA.AssignedExercisesCount.Should().Be(1);
        clinicA.RecentPatients.Should().ContainSingle()
            .Which.ClinicId.Should().Be(scenario.ClinicAId);

        var clinicB = (await controller.GetDashboard(CancellationToken.None, scenario.ClinicBId))
            .Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorDashboardDto>().Subject;
        clinicB.PatientsCount.Should().Be(1);
        clinicB.PendingRequestsCount.Should().Be(0);
        clinicB.AssignedExercisesCount.Should().Be(0);
        clinicB.RecentPatients.Single().ClinicId.Should().Be(scenario.ClinicBId);
    }

    [Fact]
    public async Task Ownership_WhenClinicIdProvided_RequiresRelationshipInThatClinic()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicAId));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var allowed = await controller.GetPatientExercises(
            scenario.Patient.Id,
            scenario.ClinicAId,
            CancellationToken.None);
        allowed.Should().BeOfType<OkObjectResult>();

        var denied = await controller.GetPatientExercises(
            scenario.Patient.Id,
            scenario.ClinicBId,
            CancellationToken.None);
        denied.Should().BeOfType<NotFoundObjectResult>();

        var unlinkedClinic = await controller.GetPatientExercises(
            scenario.Patient.Id,
            Guid.NewGuid(),
            CancellationToken.None);
        unlinkedClinic.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPatientExercises_WhenLinkedInTwoClinics_DoesNotDuplicateAssignments()
    {
        await using var host = await RelationshipTestHost.CreateAsync();
        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(
            host,
            includeSecondClinic: true);
        host.DbContext.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicAId),
            DoctorPatientBuilder.Create(scenario.Doctor.Id, scenario.Patient.Id, scenario.ClinicBId));

        var exercise = ExerciseBuilder.Create(createdByDoctorId: scenario.Doctor.Id);
        host.DbContext.Exercises.Add(exercise);
        host.DbContext.UserExercises.Add(new UserExercise
        {
            UserExerciseId = Guid.NewGuid(),
            DoctorId = scenario.Doctor.Id,
            PatientId = scenario.Patient.Id,
            ClinicId = scenario.ClinicAId,
            ExerciseId = exercise.ExerciseId,
            AssignedAt = DateTime.UtcNow,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            IsEnabled = true,
        });
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var result = await controller.GetPatientExercises(scenario.Patient.Id, scenario.ClinicAId, CancellationToken.None);

        var exercises = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<DoctorPatientExerciseDto>>().Subject;
        exercises.Should().ContainSingle();
    }
}
