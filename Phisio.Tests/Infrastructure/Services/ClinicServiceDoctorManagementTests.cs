using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Clinics;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ClinicServiceDoctorManagementTests
{
    [Fact]
    public async Task GetDoctorsAsync_WhenClinicManagerOwnsClinic_ReturnsDoctors()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetDoctorsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().Contain(member => member.DoctorId == manager.Id && member.IsClinicManager);
        result.Value.Should().Contain(member => member.DoctorId == doctor.Id && !member.IsClinicManager);
    }

    [Fact]
    public async Task GetDoctorsAsync_WhenClinicManagerAccessesAnotherClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetDoctorsAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            otherClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task GetDoctorsAsync_WhenAdminAccessesAnyClinic_ReturnsDoctors()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, admin);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetDoctorsAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Should().ContainSingle()
            .Which.IsClinicManager.Should().BeTrue();
    }

    [Fact]
    public async Task AddDoctorAsync_WhenDoctorIsValid_AddsDoctorToClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AddDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value!.DoctorId.Should().Be(doctor.Id);

        (await context.ClinicDoctors.CountAsync(link => link.ClinicId == clinic.ClinicId))
            .Should().Be(2);
    }

    [Fact]
    public async Task AddDoctorAsync_WhenDoctorAlreadyAssigned_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15554444444");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AddDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.DoctorAlreadyAssigned);
    }

    [Fact]
    public async Task AddDoctorAsync_WhenUserIsPatient_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15555555555");
        context.Users.AddRange(manager, patient);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AddDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            patient.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.DoctorCannotBeAssigned);
    }

    [Fact]
    public async Task AddDoctorAsync_WhenClinicIsDisabled_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15556666666");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Disabled Clinic");
        clinic.IsEnabled = false;
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AddDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task AddDoctorAsync_WhenClinicManagerAddsAnotherClinicManager_Succeeds()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15557777777");
        context.Users.AddRange(manager, otherManager);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AddDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            otherManager.Id);

        result.Succeeded.Should().BeTrue();
        result.Value!.Role.Should().Be(UserRole.ClinicManager);
    }

    [Fact]
    public async Task RemoveDoctorAsync_WhenRemovingRegularDoctor_Succeeds()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15558888888");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.RemoveDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeTrue();

        (await context.ClinicDoctors.CountAsync(link => link.ClinicId == clinic.ClinicId))
            .Should().Be(1);
        (await context.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == manager.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RemoveDoctorAsync_WhenRemovingClinicManager_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        context.Users.Add(manager);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.RemoveDoctorAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            manager.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.CannotRemoveClinicManager);
    }

    [Fact]
    public async Task RemoveDoctorAsync_WhenAdminRemovesDoctorFromAnyClinic_Succeeds()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15559999999");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, doctor, admin);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new Domain.Entities.ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.RemoveDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId,
            doctor.Id);

        result.Succeeded.Should().BeTrue();
    }
}
