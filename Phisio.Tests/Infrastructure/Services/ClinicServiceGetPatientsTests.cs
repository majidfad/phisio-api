using FluentAssertions;
using Phisio.Application.Clinics;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ClinicServiceGetPatientsTests
{
    [Fact]
    public async Task GetPatientsAsync_WhenClinicManagerOwnsClinic_ReturnsDoctorsPatients()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        var patientA = ApplicationUserBuilder.Patient(name: "Patient A", phoneNumber: "+15553333333");
        var patientB = ApplicationUserBuilder.Patient(name: "Patient B", phoneNumber: "+15554444444");
        context.Users.AddRange(manager, doctor, patientA, patientB);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        context.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(manager.Id, patientA.Id, clinic.ClinicId),
            DoctorPatientBuilder.Create(doctor.Id, patientB.Id, clinic.ClinicId));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetPatientsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().Contain(item =>
            item.PatientId == patientA.Id &&
            item.DoctorId == manager.Id &&
            item.ClinicId == clinic.ClinicId);
        result.Value.Should().Contain(item =>
            item.PatientId == patientB.Id &&
            item.DoctorId == doctor.Id &&
            item.DoctorName == doctor.Name);
    }

    [Fact]
    public async Task GetPatientsAsync_WhenFilteredByDoctor_ReturnsOnlyThatDoctorsPatients()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        var patientA = ApplicationUserBuilder.Patient(name: "Patient A", phoneNumber: "+15553333333");
        var patientB = ApplicationUserBuilder.Patient(name: "Patient B", phoneNumber: "+15554444444");
        context.Users.AddRange(manager, doctor, patientA, patientB);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        context.DoctorPatients.AddRange(
            DoctorPatientBuilder.Create(manager.Id, patientA.Id, clinic.ClinicId),
            DoctorPatientBuilder.Create(doctor.Id, patientB.Id, clinic.ClinicId));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetPatientsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.PatientId.Should().Be(patientB.Id);
    }

    [Fact]
    public async Task GetPatientsAsync_WhenClinicManagerAccessesAnotherClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15553333333");
        context.Users.AddRange(manager, otherManager, patient);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        otherClinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        context.DoctorPatients.Add(
            DoctorPatientBuilder.Create(otherManager.Id, patient.Id, otherClinic.ClinicId));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetPatientsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            otherClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task GetPatientsAsync_WhenAdminAccessesAnyClinic_ReturnsPatients()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var admin = ApplicationUserBuilder.Admin();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15553333333");
        context.Users.AddRange(manager, admin, patient);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.DoctorPatients.Add(
            DoctorPatientBuilder.Create(manager.Id, patient.Id, clinic.ClinicId));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetPatientsAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.PatientId.Should().Be(patient.Id);
    }

    [Fact]
    public async Task GetPatientsAsync_WhenNoApprovedLinks_ReturnsEmpty()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15553333333");
        context.Users.AddRange(manager, patient);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.DoctorPatients.Add(DoctorPatientBuilder.Create(
            manager.Id,
            patient.Id,
            clinic.ClinicId,
            status: DoctorPatientStatus.Pending));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetPatientsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
