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

public class ClinicServiceGetTests
{
    [Fact]
    public async Task GetAllAsync_WhenCallerIsClinicManager_ReturnsOnlyManagedClinics()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var managedClinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Managed Clinic");
        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.AddRange(managedClinic, otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetAllAsync(new ClinicAccessContext(manager.Id, IsAdmin: false));

        result.Succeeded.Should().BeTrue();
        result.Value!.Should().ContainSingle()
            .Which.Name.Should().Be("Managed Clinic");
    }

    [Fact]
    public async Task GetAllAsync_WhenCallerIsAdmin_ReturnsAllClinics()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, otherManager, admin);

        context.Clinics.AddRange(
            ClinicServiceTestHelper.CreateClinic(manager.Id, "Clinic A"),
            ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Clinic B"));
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetAllAsync(new ClinicAccessContext(admin.Id, IsAdmin: true));

        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenClinicManagerAccessesAnotherManagersClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetByIdAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            otherClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_WhenDoctorAccessesAnotherManagersClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15554444444");
        context.Users.AddRange(doctor, otherManager);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.GetByIdAsync(
            new ClinicAccessContext(doctor.Id, IsAdmin: false),
            otherClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.NotFound);
    }
}

public class ClinicServiceCreateTests
{
    [Fact]
    public async Task CreateAsync_WhenCallerIsClinicManager_UsesAuthenticatedUserAsManager()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var request = new CreateClinicDto
        {
            Name = "New Clinic",
            Address = "New Address",
            PhoneNumbers = ["+15553333333"],
            ClinicManagerId = otherManager.Id,
        };

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            request);

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicManagerId.Should().Be(manager.Id);

        var membership = await context.ClinicDoctors
            .SingleAsync(link => link.ClinicId == result.Value.ClinicId);

        membership.DoctorId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsClinicManager_AddsManagerDoctorMembershipAutomatically()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        context.Users.Add(manager);

        var request = new CreateClinicDto
        {
            Name = "Membership Clinic",
            Address = "Membership Address",
            PhoneNumbers = ["+15554444444"],
        };

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            request);

        result.Succeeded.Should().BeTrue();

        var clinic = await context.Clinics
            .Include(c => c.ClinicDoctors)
            .SingleAsync(c => c.ClinicId == result.Value!.ClinicId);

        clinic.HasManagerDoctorMembership().Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsDoctor_GrantsRoleAndCanManageCreatedClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15555550001");
        var colleague = ApplicationUserBuilder.Doctor(phoneNumber: "+15555550002");
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
        context.Users.AddRange(doctor, colleague);
        context.Roles.AddRange(doctorRole, clinicManagerRole);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = doctor.Id,
            RoleId = doctorRole.Id,
        });
        await context.SaveChangesAsync();

        var access = new ClinicAccessContext(doctor.Id, IsAdmin: false);
        var sut = new ClinicService(context);
        var createResult = await sut.CreateAsync(
            access,
            new CreateClinicDto
            {
                Name = "Doctor Managed Clinic",
                Address = "Doctor Address",
                PhoneNumbers = ["+15555550003"],
            });

        createResult.Succeeded.Should().BeTrue();
        createResult.Value!.ClinicManagerId.Should().Be(doctor.Id);
        doctor.Role.Should().Be(UserRole.Doctor);

        var roles = await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == doctor.Id
            select role.Name)
            .ToListAsync();
        roles.Should().Contain(RoleNames.Doctor);
        roles.Should().Contain(RoleNames.ClinicManager);

        var clinic = await context.Clinics
            .Include(item => item.ClinicDoctors)
            .SingleAsync(item => item.ClinicId == createResult.Value.ClinicId);
        clinic.HasManagerDoctorMembership().Should().BeTrue();

        var updateResult = await sut.UpdateAsync(
            access,
            clinic.ClinicId,
            new UpdateClinicDto
            {
                Name = "Updated Doctor Clinic",
                Address = clinic.Address,
                PhoneNumbers = ["+15555550003"],
            });
        updateResult.Succeeded.Should().BeTrue();

        var addDoctorResult = await sut.AddDoctorAsync(access, clinic.ClinicId, colleague.Id);
        addDoctorResult.Succeeded.Should().BeTrue();

        var deleteResult = await sut.DeleteAsync(access, clinic.ClinicId);
        deleteResult.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenPhoneNumbersAreEmpty_ReturnsRequiredError()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager();
        context.Users.Add(manager);

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            new CreateClinicDto
            {
                Name = "No Phone Clinic",
                Address = "No Phone Address",
                PhoneNumbers = [],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.PhoneNumberRequired);
        context.Clinics.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenPhoneNumberBelongsToExistingClinic_ReturnsDuplicateError()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager();
        context.Users.Add(manager);
        var existingClinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Existing Clinic");
        existingClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "+982112345678",
            NormalizedPhoneNumber = "+982112345678",
        });
        context.Clinics.Add(existingClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            new CreateClinicDto
            {
                Name = "Duplicate Clinic",
                Address = "Duplicate Address",
                PhoneNumbers = ["+982112345678"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.PhoneNumberAlreadyExists);
        context.Clinics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_WhenPhoneFormattingDiffersFromExistingClinic_ReturnsDuplicateError()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager();
        context.Users.Add(manager);
        var existingClinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Formatted Clinic");
        existingClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "+98 (21) 1234-5678",
            NormalizedPhoneNumber = "+982112345678",
        });
        context.Clinics.Add(existingClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            new CreateClinicDto
            {
                Name = "Same Number Clinic",
                Address = "Same Number Address",
                PhoneNumbers = ["98 21 1234 5678"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.PhoneNumberAlreadyExists);
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherDoctorReusesExistingClinicPhone_ReturnsDuplicateError()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15550100001");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15550100002");
        context.Users.AddRange(manager, doctor);
        var existingClinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Other Manager Clinic");
        existingClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "+1 555 010 1000",
            NormalizedPhoneNumber = "+15550101000",
        });
        context.Clinics.Add(existingClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(doctor.Id, IsAdmin: false),
            new CreateClinicDto
            {
                Name = "Doctor Duplicate Clinic",
                Address = "Doctor Duplicate Address",
                PhoneNumbers = ["1 (555) 010-1000"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.PhoneNumberAlreadyExists);
        context.Clinics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsAdmin_RequiresClinicManagerId()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        context.Users.Add(admin);

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new CreateClinicDto
            {
                Name = "Admin Clinic",
                Address = "Admin Address",
                PhoneNumbers = ["+15555555555"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.ManagerIdRequired);
    }

    [Fact]
    public async Task CreateAsync_WhenCallerIsAdmin_PromotesSelectedDoctorAndAddsMembership()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15556666666");
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
        context.Users.AddRange(admin, doctor);
        context.Roles.AddRange(doctorRole, clinicManagerRole);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = doctor.Id,
            RoleId = doctorRole.Id,
        });
        await context.SaveChangesAsync();

        var request = new CreateClinicDto
        {
            Name = "Admin Created Clinic",
            Address = "Admin Created Address",
            PhoneNumbers = ["+15557777777"],
            ClinicManagerId = doctor.Id,
        };

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            request);

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicManagerId.Should().Be(doctor.Id);

        var selectedDoctor = await context.Users.SingleAsync(user => user.Id == doctor.Id);
        selectedDoctor.Role.Should().Be(UserRole.Doctor);

        var selectedDoctorRoles = await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == doctor.Id
            select role.Name)
            .ToListAsync();
        selectedDoctorRoles.Should().Contain(RoleNames.Doctor);
        selectedDoctorRoles.Should().Contain(RoleNames.ClinicManager);

        var clinic = await context.Clinics
            .Include(item => item.ClinicDoctors)
            .SingleAsync(item => item.ClinicId == result.Value.ClinicId);
        clinic.HasManagerDoctorMembership().Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Admin)]
    public async Task CreateAsync_WhenSelectedManagerIsNotDoctor_ReturnsFailure(UserRole role)
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var selectedUser = role == UserRole.Patient
            ? ApplicationUserBuilder.Patient(phoneNumber: "+15558888888")
            : ApplicationUserBuilder.Admin(phoneNumber: "+15559999999");
        context.Users.AddRange(admin, selectedUser);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.CreateAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new CreateClinicDto
            {
                Name = "Rejected Clinic",
                Address = "Rejected Address",
                PhoneNumbers = ["+15550000001"],
                ClinicManagerId = selectedUser.Id,
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.ManagerMustBeDoctor);
        context.Clinics.Should().BeEmpty();
    }
}

public class ClinicServiceUpdateDeleteTests
{
    [Fact]
    public async Task UpdateAsync_WhenClinicManagerUpdatesOwnClinic_DoesNotChangeClinicManagerId()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        context.Users.Add(manager);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Original Name");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.UpdateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId,
            new UpdateClinicDto
            {
                Name = "Updated Name",
                Address = "Updated Address",
                PhoneNumbers = ["+15558888888"],
            });

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicManagerId.Should().Be(manager.Id);
        result.Value.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_WhenClinicManagerUpdatesAnotherManagersClinic_ReturnsNotFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var otherManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(manager, otherManager);

        var otherClinic = ClinicServiceTestHelper.CreateClinic(otherManager.Id, "Other Clinic");
        context.Clinics.Add(otherClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.UpdateAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            otherClinic.ClinicId,
            new UpdateClinicDto
            {
                Name = "Blocked",
                Address = "Blocked",
                PhoneNumbers = ["+15559999999"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(ClinicErrors.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenClinicManagerDeletesOwnClinic_SoftDeletesClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        context.Users.Add(manager);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Delete Me");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.DeleteAsync(
            new ClinicAccessContext(manager.Id, IsAdmin: false),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();

        var deleted = await context.Clinics.IgnoreQueryFilters()
            .SingleAsync(c => c.ClinicId == clinic.ClinicId);

        deleted.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenAdminDeletesAnyClinic_Succeeds()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var admin = ApplicationUserBuilder.Admin();
        context.Users.AddRange(manager, admin);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Admin Delete");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.DeleteAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
    }
}

internal static class ClinicServiceTestHelper
{
    internal static Clinic CreateClinic(Guid clinicManagerId, string name) =>
        new()
        {
            ClinicId = Guid.NewGuid(),
            ClinicManagerId = clinicManagerId,
            Name = name,
            Address = $"{name} Address",
        };

    internal static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
