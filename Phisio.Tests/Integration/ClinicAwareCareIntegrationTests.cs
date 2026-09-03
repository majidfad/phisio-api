using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Assignments;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class ClinicAwareCareIntegrationTests
{
    [Fact]
    public async Task AssignPatientExercises_WhenPatientLinkedToOtherClinic_Rejects()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host, includeSecondClinic: true);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = new AssignPatientExercisesRequest(
            [new AssignPatientExerciseItem(scenario.DoctorExercise.ExerciseId, Sets: 3, Reps: "10", null, null)],
            [ExerciseManagementTestHelpers.Today]);

        (await controller.AssignPatientExercises(
            scenario.Patient.Id,
            request,
            scenario.ClinicAId,
            CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var otherClinicResult = await controller.AssignPatientExercises(
            scenario.Patient.Id,
            request,
            scenario.ClinicBId,
            CancellationToken.None);

        otherClinicResult.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(1);
        (await host.DbContext.UserExercises.SingleAsync()).ClinicId.Should().Be(scenario.ClinicAId);
    }

    [Fact]
    public async Task GetTodayExercises_WhenFilteredByClinic_ReturnsOnlyMatchingClinic()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host, includeSecondClinic: true);
        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = new AssignPatientExercisesRequest(
            [new AssignPatientExerciseItem(scenario.DoctorExercise.ExerciseId, Sets: 2, Reps: "8", null, null)],
            [ExerciseManagementTestHelpers.Today]);

        await doctorController.AssignPatientExercises(
            scenario.Patient.Id,
            request,
            scenario.ClinicAId,
            CancellationToken.None);

        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);

        var clinicAOnly = await patientController.GetTodayExercises(
            scenario.Doctor.Id,
            scenario.ClinicAId,
            CancellationToken.None);
        var clinicABody = clinicAOnly.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject;

        clinicABody.DoctorGroups.Should().ContainSingle();
        clinicABody.DoctorGroups[0].ClinicId.Should().Be(scenario.ClinicAId);
        clinicABody.DoctorGroups[0].Exercises.Should().ContainSingle()
            .Which.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);

        var clinicBOnly = await patientController.GetTodayExercises(
            scenario.Doctor.Id,
            scenario.ClinicBId,
            CancellationToken.None);
        var clinicBBody = clinicBOnly.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PatientTodayExercisesResponse>().Subject;

        clinicBBody.DoctorGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAssignment_WithoutApprovedClinicRelationship_Rejects()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var outsiderClinic = ClinicBuilder.Create(managerId: scenario.Doctor.Id, name: "Outsider Clinic");
        host.DbContext.Clinics.Add(outsiderClinic);
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.CreateAssignmentRequest(
            scenario.Patient.Id,
            scenario.AdminExercise.ExerciseId,
            outsiderClinic.ClinicId);

        var result = await controller.CreateAssignment(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeactivateAssignment_WhenDoctorLacksClinicAccess_Rejects()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host, includeSecondClinic: true);
        var assignmentsController = host.CreateAssignmentsController(scenario.Doctor.Id, RoleNames.Doctor);
        await assignmentsController.CreateAssignment(
            ExerciseManagementTestHelpers.CreateAssignmentRequest(
                scenario.Patient.Id,
                scenario.AdminExercise.ExerciseId,
                scenario.ClinicAId),
            CancellationToken.None);

        host.DbContext.ChangeTracker.Clear();
        var assignment = await host.DbContext.UserExercises.SingleAsync();

        host.DbContext.ChangeTracker.Clear();
        var link = await host.DbContext.DoctorPatients.SingleAsync(item =>
            item.DoctorId == scenario.Doctor.Id
            && item.PatientId == scenario.Patient.Id
            && item.ClinicId == scenario.ClinicAId);
        link.IsEnabled = false;
        await host.DbContext.SaveChangesAsync();

        var result = await assignmentsController.DeactivateAssignment(
            assignment.UserExerciseId,
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
