using FluentAssertions;
using Phisio.Application.Clinics;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ClinicServiceAdherenceTests
{
    [Fact]
    public async Task GetAdherenceAsync_WhenAssignmentsAndCompletionsExist_ComputesPeriods()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var patient = ApplicationUserBuilder.Patient(name: "Patient A", phoneNumber: "+15553333333");
        context.Users.AddRange(manager, patient);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exerciseId = Guid.NewGuid();
        var todayAssignment = AssignmentBuilder.Create(
            manager.Id,
            patient.Id,
            exerciseId,
            scheduledDate: today,
            clinicId: clinic.ClinicId);
        var yesterdayAssignment = AssignmentBuilder.Create(
            manager.Id,
            patient.Id,
            exerciseId,
            scheduledDate: today.AddDays(-1),
            clinicId: clinic.ClinicId);
        context.UserExercises.AddRange(todayAssignment, yesterdayAssignment);
        context.ExerciseCompletions.Add(ExerciseCompletionBuilder.Create(
            todayAssignment.UserExerciseId,
            patient.Id,
            manager.Id,
            exerciseId,
            completionDate: today));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetAdherenceAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Today.ScheduledDays.Should().Be(1);
        result.Value.Today.CompletedDays.Should().Be(1);
        result.Value.Today.AdherencePercentage.Should().Be(100);

        result.Value.Last7Days.ScheduledDays.Should().Be(2);
        result.Value.Last7Days.CompletedDays.Should().Be(1);
        result.Value.Last7Days.AdherencePercentage.Should().Be(50);

        result.Value.Patients.Should().ContainSingle();
        result.Value.Patients[0].PatientId.Should().Be(patient.Id);
        result.Value.Patients[0].AdherencePercentage.Should().Be(50);
    }

    [Fact]
    public async Task GetAdherenceAsync_WhenFilteredByDoctor_ScopesMetrics()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        var patientA = ApplicationUserBuilder.Patient(name: "Patient A", phoneNumber: "+15553333333");
        var patientB = ApplicationUserBuilder.Patient(name: "Patient B", phoneNumber: "+15554444444");
        context.Users.AddRange(manager, otherDoctor, patientA, patientB);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = otherDoctor.Id,
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exerciseId = Guid.NewGuid();
        context.UserExercises.AddRange(
            AssignmentBuilder.Create(
                manager.Id,
                patientA.Id,
                exerciseId,
                scheduledDate: today,
                clinicId: clinic.ClinicId),
            AssignmentBuilder.Create(
                otherDoctor.Id,
                patientB.Id,
                exerciseId,
                scheduledDate: today,
                clinicId: clinic.ClinicId));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetAdherenceAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            otherDoctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value!.Today.ScheduledDays.Should().Be(1);
        result.Value.Patients.Should().ContainSingle()
            .Which.PatientId.Should().Be(patientB.Id);
    }

    [Fact]
    public async Task GetAdherenceAsync_WhenClinicManagerAccessesAnotherClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetAdherenceAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            otherClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be(ClinicErrors.NotFound);
    }
}
