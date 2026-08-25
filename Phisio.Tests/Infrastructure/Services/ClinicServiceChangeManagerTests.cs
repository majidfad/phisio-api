using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ClinicServiceChangeManagerTests
{
    [Fact]
    public async Task ChangeManagerAsync_AsAdmin_UpdatesManagerAndTransfersRoles()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var (doctorRole, clinicManagerRole) = await SeedDoctorAndClinicManagerRolesAsync(context);

        var previousManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15551111111");
        var newManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        context.Users.AddRange(previousManager, newManager);
        context.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = doctorRole.Id },
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = clinicManagerRole.Id },
            new IdentityUserRole<Guid> { UserId = newManager.Id, RoleId = doctorRole.Id });

        var clinic = ClinicServiceTestHelper.CreateClinic(previousManager.Id, "Managed Clinic");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var admin = ApplicationUserBuilder.Admin();
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = newManager.Id });

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicManagerId.Should().Be(newManager.Id);

        var persisted = await context.Clinics.SingleAsync(item => item.ClinicId == clinic.ClinicId);
        persisted.ClinicManagerId.Should().Be(newManager.Id);

        (await HasIdentityRoleAsync(context, previousManager.Id, RoleNames.ClinicManager))
            .Should().BeFalse();
        (await HasIdentityRoleAsync(context, newManager.Id, RoleNames.ClinicManager))
            .Should().BeTrue();
        (await HasIdentityRoleAsync(context, previousManager.Id, RoleNames.Doctor))
            .Should().BeTrue();
        (await HasIdentityRoleAsync(context, newManager.Id, RoleNames.Doctor))
            .Should().BeTrue();

        previousManager.Role.Should().Be(UserRole.Doctor);
        newManager.Role.Should().Be(UserRole.Doctor);
    }

    [Fact]
    public async Task ChangeManagerAsync_KeepsPreviousManagerRoleWhenStillManagingAnotherClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var (doctorRole, clinicManagerRole) = await SeedDoctorAndClinicManagerRolesAsync(context);

        var previousManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15551111111");
        var newManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        context.Users.AddRange(previousManager, newManager);
        context.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = doctorRole.Id },
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = clinicManagerRole.Id },
            new IdentityUserRole<Guid> { UserId = newManager.Id, RoleId = doctorRole.Id });

        var firstClinic = ClinicServiceTestHelper.CreateClinic(previousManager.Id, "Clinic A");
        firstClinic.EnsureManagerDoctorMembership();
        var secondClinic = ClinicServiceTestHelper.CreateClinic(previousManager.Id, "Clinic B");
        secondClinic.EnsureManagerDoctorMembership();
        context.Clinics.AddRange(firstClinic, secondClinic);
        await context.SaveChangesAsync();

        var admin = ApplicationUserBuilder.Admin();
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            firstClinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = newManager.Id });

        result.Succeeded.Should().BeTrue();
        (await HasIdentityRoleAsync(context, previousManager.Id, RoleNames.ClinicManager))
            .Should().BeTrue("previous manager still manages another clinic");
    }

    [Fact]
    public async Task ChangeManagerAsync_PreviousManagerRemainsClinicDoctorMember()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var (doctorRole, clinicManagerRole) = await SeedDoctorAndClinicManagerRolesAsync(context);

        var previousManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15551111111");
        var newManager = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        context.Users.AddRange(previousManager, newManager);
        context.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = doctorRole.Id },
            new IdentityUserRole<Guid> { UserId = previousManager.Id, RoleId = clinicManagerRole.Id },
            new IdentityUserRole<Guid> { UserId = newManager.Id, RoleId = doctorRole.Id });

        var clinic = ClinicServiceTestHelper.CreateClinic(previousManager.Id, "Managed Clinic");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var admin = ApplicationUserBuilder.Admin();
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = newManager.Id });

        (await context.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == previousManager.Id))
            .Should().BeTrue();
        (await context.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == newManager.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ChangeManagerAsync_WhenNotAdmin_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Managed Clinic");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = doctor.Id });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.AdminRequired);
        (await context.Clinics.SingleAsync()).ClinicManagerId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task ChangeManagerAsync_WhenManagerNotDoctor_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15553333333");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, patient, admin);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Managed Clinic");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = patient.Id });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.ManagerMustBeDoctor);
    }

    [Fact]
    public async Task ChangeManagerAsync_WhenClinicNotFound_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var doctor = ApplicationUserBuilder.Doctor();
        context.Users.AddRange(admin, doctor);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            Guid.NewGuid(),
            new ChangeClinicManagerDto { ClinicManagerId = doctor.Id });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task ChangeManagerAsync_WhenSameManager_IsIdempotent()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, admin);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Managed Clinic");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.ChangeManagerAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = manager.Id });

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicManagerId.Should().Be(manager.Id);
    }

    private static async Task<(ApplicationRole DoctorRole, ApplicationRole ClinicManagerRole)> SeedDoctorAndClinicManagerRolesAsync(
        AppDbContext context)
    {
        var doctorRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = RoleNames.Doctor,
            NormalizedName = RoleNames.Doctor.ToUpperInvariant(),
        };
        var clinicManagerRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = RoleNames.ClinicManager,
            NormalizedName = RoleNames.ClinicManager.ToUpperInvariant(),
        };
        context.Roles.AddRange(doctorRole, clinicManagerRole);
        await context.SaveChangesAsync();
        return (doctorRole, clinicManagerRole);
    }

    private static async Task<bool> HasIdentityRoleAsync(
        AppDbContext context,
        Guid userId,
        string roleName)
    {
        return await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name == roleName
            select userRole)
            .AnyAsync();
    }
}
