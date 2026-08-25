using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class ClinicManagementIntegrationTests
{
    // 1. Create Clinic as Admin
    [Fact]
    public async Task CreateClinic_AsAdmin_WithValidData_CreatesClinicAndPhones()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var doctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);

        var request = new CreateClinicDto
        {
            Name = "Vanak Clinic",
            Address = "Vanak St 1",
            PhoneNumbers = ["02188881111"],
            ClinicManagerId = doctor.Id,
        };

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<ClinicDto>().Subject;
        body.Name.Should().Be(request.Name);
        body.Address.Should().Be(request.Address);
        body.ClinicManagerId.Should().Be(doctor.Id);
        body.PhoneNumbers.Should().ContainSingle()
            .Which.Should().Be("02188881111");

        var clinic = await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync();
        clinic.ClinicId.Should().Be(body.ClinicId);
        clinic.Name.Should().Be(request.Name);
        clinic.Address.Should().Be(request.Address);
        clinic.ClinicManagerId.Should().Be(doctor.Id);

        var phones = await host.DbContext.ClinicPhoneNumbers.ToListAsync();
        phones.Should().ContainSingle();
        phones[0].ClinicId.Should().Be(clinic.ClinicId);
        phones[0].PhoneNumber.Should().Be("02188881111");
        phones[0].NormalizedPhoneNumber.Should().Be(PhoneNumberNormalizer.Normalize("02188881111"));
    }

    // 2. Create Clinic with Doctor as Manager
    [Fact]
    public async Task CreateClinic_AsAdmin_WithDoctorManager_AddsClinicManagerRoleKeepsDoctorRole()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var doctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Manager Clinic",
                Address = "Mgr Address",
                PhoneNumbers = ["02188882222"],
                ClinicManagerId = doctor.Id,
            },
            CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();

        var persistedDoctor = await host.DbContext.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.Id == doctor.Id);
        persistedDoctor.Role.Should().Be(UserRole.Doctor);

        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.Doctor))
            .Should().BeTrue();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.ClinicManager))
            .Should().BeTrue();

        var membership = await host.DbContext.ClinicDoctors.SingleAsync();
        membership.DoctorId.Should().Be(doctor.Id);
    }

    // 3. Create Clinic with invalid Manager
    [Fact]
    public async Task CreateClinic_AsAdmin_WhenManagerDoesNotExist_Rejects()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "No Manager Clinic",
                Address = "Address",
                PhoneNumbers = ["02188883333"],
                ClinicManagerId = Guid.NewGuid(),
            },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ManagerNotFound);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateClinic_AsAdmin_WhenManagerIsNotDoctor_Rejects()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var patient = await ClinicManagementTestHostSeeder.SeedPatientAsync(host);

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Patient Manager Clinic",
                Address = "Address",
                PhoneNumbers = ["02188884444"],
                ClinicManagerId = patient.Id,
            },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ManagerMustBeDoctor);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateClinic_AsAdmin_WhenManagerIdMissing_Rejects()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Missing Manager",
                Address = "Address",
                PhoneNumbers = ["02188885555"],
                ClinicManagerId = null,
            },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ManagerIdRequired);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 4. Duplicate Clinic phone number
    [Fact]
    public async Task CreateClinic_WhenPhoneAlreadyUsed_RejectsAndCreatesNoDuplicate()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(
            host,
            phoneNumber: "02199990001");
        var anotherDoctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Other",
            phoneNumber: "+15552000999");

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Duplicate Phone Clinic",
                Address = "Other Address",
                PhoneNumbers = ["02199990001"],
                ClinicManagerId = anotherDoctor.Id,
            },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.PhoneNumberAlreadyExists);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await host.DbContext.ClinicPhoneNumbers.CountAsync()).Should().Be(1);
    }

    // 5. Multiple Clinic phone numbers
    [Fact]
    public async Task CreateClinic_WithMultiplePhoneNumbers_SavesAll()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var doctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);

        var phones = new[] { "02170001111", "02170002222", "09120001111" };
        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Multi Phone Clinic",
                Address = "Multi Address",
                PhoneNumbers = phones,
                ClinicManagerId = doctor.Id,
            },
            CancellationToken.None);

        var body = result.Should().BeOfType<CreatedAtActionResult>().Subject
            .Value.Should().BeOfType<ClinicDto>().Subject;
        body.PhoneNumbers.Should().BeEquivalentTo(phones);

        var saved = await host.DbContext.ClinicPhoneNumbers.ToListAsync();
        saved.Should().HaveCount(3);
        saved.Select(phone => phone.PhoneNumber).Should().BeEquivalentTo(phones);
        saved.Should().OnlyContain(phone => phone.ClinicId == body.ClinicId);
    }

    // 6. Edit Clinic
    [Fact]
    public async Task UpdateClinic_AsAdmin_UpdatesNameAndAddress()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var originalPhone = (await host.DbContext.ClinicPhoneNumbers.SingleAsync()).PhoneNumber;

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.UpdateClinic(
            seed.ClinicId,
            new UpdateClinicDto
            {
                Name = "Updated Clinic Name",
                Address = "Updated Address",
                PhoneNumbers = [originalPhone],
            },
            CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ClinicDto>().Subject;
        body.Name.Should().Be("Updated Clinic Name");
        body.Address.Should().Be("Updated Address");

        var clinic = await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync();
        clinic.Name.Should().Be("Updated Clinic Name");
        clinic.Address.Should().Be("Updated Address");
        clinic.ClinicManagerId.Should().Be(seed.ManagerDoctor.Id);
    }

    // 7. Edit Clinic phone numbers
    [Fact]
    public async Task UpdateClinic_CanAddRemoveAndRejectDuplicatePhones()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(
            host,
            phoneNumber: "02111110001");

        var otherSeedPhone = "02122220002";
        var otherDoctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Second",
            phoneNumber: "+15552000088");
        var otherClinic = ClinicBuilder.Create(managerId: otherDoctor.Id, name: "Other Clinic");
        host.DbContext.Clinics.Add(otherClinic);
        host.DbContext.ClinicPhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            ClinicId = otherClinic.ClinicId,
            PhoneNumber = otherSeedPhone,
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize(otherSeedPhone),
        });
        host.DbContext.ClinicDoctors.Add(ClinicBuilder.CreateMembership(otherClinic.ClinicId, otherDoctor.Id));
        await host.DbContext.SaveChangesAsync();

        var controller = host.CreateAdminController(seed.Admin.Id);

        // Replace original phone and add a new one
        var updated = await controller.UpdateClinic(
            seed.ClinicId,
            new UpdateClinicDto
            {
                Name = seed.Clinic!.Name,
                Address = seed.Clinic.Address,
                PhoneNumbers = ["02111110099", "02111110088"],
            },
            CancellationToken.None);

        var body = updated.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ClinicDto>().Subject;
        body.PhoneNumbers.Should().BeEquivalentTo(["02111110099", "02111110088"]);

        var phones = await host.DbContext.ClinicPhoneNumbers
            .Where(phone => phone.ClinicId == seed.ClinicId)
            .Select(phone => phone.PhoneNumber)
            .ToListAsync();
        phones.Should().BeEquivalentTo(["02111110099", "02111110088"]);

        // Duplicate phone from another clinic must be rejected
        var duplicate = await controller.UpdateClinic(
            seed.ClinicId,
            new UpdateClinicDto
            {
                Name = seed.Clinic.Name,
                Address = seed.Clinic.Address,
                PhoneNumbers = [otherSeedPhone],
            },
            CancellationToken.None);

        var badRequest = duplicate.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.PhoneNumberAlreadyExists);

        (await host.DbContext.ClinicPhoneNumbers
            .Where(phone => phone.ClinicId == seed.ClinicId)
            .Select(phone => phone.PhoneNumber)
            .ToListAsync())
            .Should().BeEquivalentTo(["02111110099", "02111110088"]);
    }

    // 8. Add Doctor to Clinic
    [Fact]
    public async Task AddClinicDoctor_WhenValid_CreatesClinicDoctorRelationship()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. New Member",
            phoneNumber: "+15552000033");

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.AddClinicDoctor(
            seed.ClinicId,
            member.Id,
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<ClinicDoctorMemberDto>().Subject;
        body.DoctorId.Should().Be(member.Id);

        var links = await host.DbContext.ClinicDoctors
            .Where(link => link.ClinicId == seed.ClinicId)
            .ToListAsync();
        links.Should().HaveCount(2);
        links.Select(link => link.DoctorId).Should().BeEquivalentTo(
            [seed.ManagerDoctor.Id, member.Id]);
    }

    // 9. Add Doctor who is already in the Clinic
    [Fact]
    public async Task AddClinicDoctor_WhenAlreadyAssigned_RejectsDuplicate()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(
            host,
            includeMemberDoctor: true);

        var beforeCount = await host.DbContext.ClinicDoctors.CountAsync();
        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.AddClinicDoctor(
            seed.ClinicId,
            seed.MemberDoctor!.Id,
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.DoctorAlreadyAssigned);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(beforeCount);
    }

    // 10. Remove Doctor from Clinic
    [Fact]
    public async Task RemoveClinicDoctor_WhenMember_RemovesRelationshipAndKeepsDoctorUser()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(
            host,
            includeMemberDoctor: true);

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.RemoveClinicDoctor(
            seed.ClinicId,
            seed.MemberDoctor!.Id,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        (await host.DbContext.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == seed.ClinicId && link.DoctorId == seed.MemberDoctor.Id))
            .Should().BeFalse();

        var doctor = await host.DbContext.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.Id == seed.MemberDoctor.Id);
        doctor.Role.Should().Be(UserRole.Doctor);
        doctor.IsEnabled.Should().BeTrue();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.Doctor))
            .Should().BeTrue();
    }

    // 11. Clinic Manager adds Doctor
    [Fact]
    public async Task AddClinicDoctor_AsOwnClinicManager_Succeeds()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Added By Manager",
            phoneNumber: "+15552000044");

        var controller = host.CreateManagerController(seed.ManagerDoctor.Id);
        var result = await controller.AddClinicDoctor(
            seed.ClinicId,
            member.Id,
            CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        (await host.DbContext.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == seed.ClinicId && link.DoctorId == member.Id))
            .Should().BeTrue();
    }

    // 12. Clinic Manager manages another Clinic
    [Fact]
    public async Task ClinicManager_CannotManageAnotherClinic()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);

        var otherManager = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Other Manager",
            phoneNumber: "+15552000055",
            grantClinicManagerIdentityRole: true);
        var otherClinic = ClinicBuilder.Create(managerId: otherManager.Id, name: "Other Managed Clinic");
        host.DbContext.Clinics.Add(otherClinic);
        host.DbContext.ClinicDoctors.Add(ClinicBuilder.CreateMembership(otherClinic.ClinicId, otherManager.Id));
        await host.DbContext.SaveChangesAsync();

        var outsider = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Outsider",
            phoneNumber: "+15552000066");

        var controller = host.CreateManagerController(seed.ManagerDoctor.Id);

        var getOther = await controller.GetClinic(otherClinic.ClinicId, CancellationToken.None);
        getOther.Should().BeOfType<NotFoundObjectResult>();

        var addToOther = await controller.AddClinicDoctor(
            otherClinic.ClinicId,
            outsider.Id,
            CancellationToken.None);
        addToOther.Should().BeOfType<NotFoundObjectResult>();

        var updateOther = await controller.UpdateClinic(
            otherClinic.ClinicId,
            new UpdateClinicDto
            {
                Name = "Hacked",
                Address = "Hacked",
                PhoneNumbers = ["02100009999"],
            },
            CancellationToken.None);
        updateOther.Should().BeOfType<NotFoundObjectResult>();

        (await host.DbContext.ClinicDoctors.CountAsync(link => link.ClinicId == otherClinic.ClinicId))
            .Should().Be(1);
        (await host.DbContext.Clinics.IgnoreQueryFilters()
            .SingleAsync(clinic => clinic.ClinicId == otherClinic.ClinicId))
            .Name.Should().Be("Other Managed Clinic");
    }

    // 13. Admin manages any Clinic
    [Fact]
    public async Task Admin_CanManageAnyClinic()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Admin Added",
            phoneNumber: "+15552000077");

        var controller = host.CreateAdminController(seed.Admin.Id);

        var get = await controller.GetClinic(seed.ClinicId, CancellationToken.None);
        get.Should().BeOfType<OkObjectResult>();

        var update = await controller.UpdateClinic(
            seed.ClinicId,
            new UpdateClinicDto
            {
                Name = "Admin Updated",
                Address = seed.Clinic!.Address,
                PhoneNumbers = ["02111110001"],
            },
            CancellationToken.None);
        update.Should().BeOfType<OkObjectResult>();

        var add = await controller.AddClinicDoctor(seed.ClinicId, member.Id, CancellationToken.None);
        add.Should().BeOfType<CreatedAtActionResult>();

        (await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync()).Name.Should().Be("Admin Updated");
        (await host.DbContext.ClinicDoctors.AnyAsync(link => link.DoctorId == member.Id)).Should().BeTrue();
    }

    // 14. Doctor becomes Clinic Manager
    [Fact]
    public async Task CreateClinic_PromotesDoctorToClinicManager_WithoutRemovingDoctorRole()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var doctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);

        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.ClinicManager))
            .Should().BeFalse();

        var controller = host.CreateAdminController(admin.Id);
        var result = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Promotion Clinic",
                Address = "Promo Address",
                PhoneNumbers = ["02133330001"],
                ClinicManagerId = doctor.Id,
            },
            CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();

        var user = await host.DbContext.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == doctor.Id);
        user.Role.Should().Be(UserRole.Doctor);
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.Doctor))
            .Should().BeTrue();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.ClinicManager))
            .Should().BeTrue();

        var clinic = await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync();
        clinic.ClinicManagerId.Should().Be(doctor.Id);
    }

    // 15. Clinic Manager reassignment and role revocation
    [Fact]
    public async Task ChangeClinicManager_AsAdmin_ReassignsManagerAndRevokesPreviousManagerRole()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(
            host,
            includeMemberDoctor: true);
        var newManager = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Successor",
            phoneNumber: "+15552000999");

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.ChangeClinicManager(
            seed.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = newManager.Id },
            CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ClinicDto>().Subject;
        body.ClinicManagerId.Should().Be(newManager.Id);

        var clinic = await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync();
        clinic.ClinicManagerId.Should().Be(newManager.Id);

        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(
            host,
            seed.ManagerDoctor.Id,
            RoleNames.ClinicManager))
            .Should().BeFalse();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(
            host,
            newManager.Id,
            RoleNames.ClinicManager))
            .Should().BeTrue();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(
            host,
            seed.ManagerDoctor.Id,
            RoleNames.Doctor))
            .Should().BeTrue();
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(
            host,
            newManager.Id,
            RoleNames.Doctor))
            .Should().BeTrue();

        seed.ManagerDoctor.Role.Should().Be(UserRole.Doctor);
        newManager.Role.Should().Be(UserRole.Doctor);

        (await host.DbContext.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == seed.ClinicId && link.DoctorId == seed.ManagerDoctor.Id))
            .Should().BeTrue();
        (await host.DbContext.ClinicDoctors.AnyAsync(link =>
            link.ClinicId == seed.ClinicId && link.DoctorId == newManager.Id))
            .Should().BeTrue();

        var removeManager = await controller.RemoveClinicDoctor(
            seed.ClinicId,
            seed.ManagerDoctor.Id,
            CancellationToken.None);
        removeManager.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ChangeClinicManager_AsClinicManager_IsRejected()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var newManager = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Blocked",
            phoneNumber: "+15552000888");

        var controller = host.CreateManagerController(seed.ManagerDoctor.Id);
        var result = await controller.ChangeClinicManager(
            seed.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = newManager.Id },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.AdminRequired);
        (await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync())
            .ClinicManagerId.Should().Be(seed.ManagerDoctor.Id);
    }

    [Fact]
    public async Task ChangeClinicManager_WhenNewManagerIsInvalid_RejectsWithoutChanges()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var patient = await ClinicManagementTestHostSeeder.SeedPatientAsync(host);

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.ChangeClinicManager(
            seed.ClinicId,
            new ChangeClinicManagerDto { ClinicManagerId = patient.Id },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ManagerMustBeDoctor);
        (await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync())
            .ClinicManagerId.Should().Be(seed.ManagerDoctor.Id);
    }

    [Fact]
    public async Task UpdateClinic_DoesNotChangeClinicManager()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var phone = (await host.DbContext.ClinicPhoneNumbers
            .SingleAsync(item => item.ClinicId == seed.ClinicId)).PhoneNumber;

        var controller = host.CreateAdminController(seed.Admin.Id);
        var update = await controller.UpdateClinic(
            seed.ClinicId,
            new UpdateClinicDto
            {
                Name = "Still Same Manager",
                Address = "New Address",
                PhoneNumbers = [phone],
            },
            CancellationToken.None);

        update.Should().BeOfType<OkObjectResult>();
        (await host.DbContext.Clinics.IgnoreQueryFilters().SingleAsync())
            .ClinicManagerId.Should().Be(seed.ManagerDoctor.Id);
    }

    [Fact]
    public async Task RemoveClinicDoctor_WhenTargetIsCurrentManager_ReturnsBadRequest()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var controller = host.CreateAdminController(seed.Admin.Id);

        var result = await controller.RemoveClinicDoctor(
            seed.ClinicId,
            seed.ManagerDoctor.Id,
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.CannotRemoveClinicManager);
    }

    // 16. Invalid Clinic
    [Fact]
    public async Task GetUpdateAdd_WhenClinicDoesNotExist_ReturnsNotFoundWithoutChanges()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var missingClinicId = Guid.NewGuid();
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Unused",
            phoneNumber: "+15552000111");

        var beforeClinics = await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync();
        var beforeLinks = await host.DbContext.ClinicDoctors.CountAsync();
        var controller = host.CreateAdminController(seed.Admin.Id);

        (await controller.GetClinic(missingClinicId, CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();

        var update = await controller.UpdateClinic(
            missingClinicId,
            new UpdateClinicDto
            {
                Name = "Nope",
                Address = "Nope",
                PhoneNumbers = ["02100000001"],
            },
            CancellationToken.None);
        update.Should().BeOfType<NotFoundObjectResult>();

        var add = await controller.AddClinicDoctor(missingClinicId, member.Id, CancellationToken.None);
        add.Should().BeOfType<NotFoundObjectResult>();

        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(beforeClinics);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(beforeLinks);
    }

    // 17. Invalid Doctor
    [Fact]
    public async Task AddClinicDoctor_WhenDoctorDoesNotExist_ReturnsErrorAndCreatesNoLink()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var beforeLinks = await host.DbContext.ClinicDoctors.CountAsync();

        var controller = host.CreateAdminController(seed.Admin.Id);
        var result = await controller.AddClinicDoctor(
            seed.ClinicId,
            Guid.NewGuid(),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.DoctorNotFound);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(beforeLinks);
    }

    // 18. Authorization
    [Fact]
    public async Task Authorization_ClinicManagementPolicy_RejectsAnonymousPatientAndUnprivilegedDoctor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ClinicManagement, policy =>
                policy.RequireRole(RoleNames.Admin, RoleNames.ClinicManager, RoleNames.Doctor));
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
        var clinicManager = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.ClinicManager)],
            authenticationType: "Test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.ClinicManagement))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.ClinicManagement))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.ClinicManagement))
            .Succeeded.Should().BeTrue();
        (await authorizationService.AuthorizeAsync(
            clinicManager, resource: null, AuthorizationPolicies.ClinicManagement))
            .Succeeded.Should().BeTrue();
        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.ClinicManagement))
            .Succeeded.Should().BeTrue();

        typeof(ClinicsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.ClinicManagement);
    }

    [Fact]
    public async Task Authorization_Controllers_RejectMissingUserAndCrossClinicAccess()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);

        var anonymousController = host.CreateClinicsController(userId: null);
        (await anonymousController.GetClinics(cancellationToken: CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
        (await anonymousController.CreateClinic(
            new CreateClinicDto
            {
                Name = "X",
                Address = "Y",
                PhoneNumbers = ["02100001111"],
                ClinicManagerId = seed.ManagerDoctor.Id,
            },
            CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();

        // Doctor who is not the manager of this clinic cannot mutate it.
        var plainDoctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Non Manager",
            phoneNumber: "+15552000222");
        var doctorController = host.CreateDoctorController(plainDoctor.Id);
        (await doctorController.GetClinic(seed.ClinicId, CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();
        (await doctorController.AddClinicDoctor(
            seed.ClinicId,
            plainDoctor.Id,
            CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();
    }

    // 19. Data integrity
    [Fact]
    public async Task DataIntegrity_ClinicPhonesAndDoctors_AreConsistent()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var manager = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Integrity",
            phoneNumber: "+15552000333");

        var controller = host.CreateAdminController(admin.Id);
        var create = await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Integrity Clinic",
                Address = "Integrity Address",
                PhoneNumbers = ["02144440001", "02144440002"],
                ClinicManagerId = manager.Id,
            },
            CancellationToken.None);
        var clinic = create.Should().BeOfType<CreatedAtActionResult>().Subject
            .Value.Should().BeOfType<ClinicDto>().Subject;

        await controller.AddClinicDoctor(clinic.ClinicId, member.Id, CancellationToken.None);

        var persisted = await host.DbContext.Clinics.IgnoreQueryFilters()
            .Include(item => item.PhoneNumbers)
            .Include(item => item.ClinicDoctors)
            .SingleAsync();

        persisted.ClinicId.Should().Be(clinic.ClinicId);
        persisted.ClinicManagerId.Should().Be(manager.Id);
        persisted.PhoneNumbers.Should().HaveCount(2);
        persisted.ClinicDoctors.Select(link => link.DoctorId)
            .Should().BeEquivalentTo([manager.Id, member.Id]);

        foreach (var phone in persisted.PhoneNumbers)
        {
            phone.ClinicId.Should().Be(persisted.ClinicId);
            phone.NormalizedPhoneNumber.Should().Be(PhoneNumberNormalizer.Normalize(phone.PhoneNumber));
        }

        // Unique phone index exists on the model.
        var phoneEntity = host.DbContext.Model.FindEntityType(typeof(ClinicPhoneNumber));
        phoneEntity.Should().NotBeNull();
        phoneEntity!.GetIndexes()
            .Any(index => index.IsUnique
                && index.Properties.Any(property => property.Name == nameof(ClinicPhoneNumber.NormalizedPhoneNumber)))
            .Should().BeTrue();

        var doctorLinkEntity = host.DbContext.Model.FindEntityType(typeof(ClinicDoctor));
        doctorLinkEntity!.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().BeEquivalentTo(["ClinicId", "DoctorId"]);
    }

    // 20. Transaction / rollback
    [Fact]
    public async Task CreateClinic_WhenSaveFails_LeavesNoPartialClinicOrRoleRecords()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync(services =>
            services.UseFailingClinicSaveInterceptor());

        var interceptor = host.GetRequiredService<FailingClinicSaveInterceptor>();
        var admin = await ClinicManagementTestHostSeeder.SeedAdminAsync(host);
        var doctor = await ClinicManagementTestHostSeeder.SeedDoctorAsync(host);

        interceptor.FailOnNextClinicRelatedSave = true;
        var controller = host.CreateAdminController(admin.Id);

        var act = async () => await controller.CreateClinic(
            new CreateClinicDto
            {
                Name = "Rollback Clinic",
                Address = "Rollback Address",
                PhoneNumbers = ["02155550001"],
                ClinicManagerId = doctor.Id,
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated clinic persistence failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicPhoneNumbers.CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(0);
        (await ClinicManagementTestHostSeeder.HasIdentityRoleAsync(host, doctor.Id, RoleNames.ClinicManager))
            .Should().BeFalse();
    }

    [Fact]
    public async Task AddClinicDoctor_WhenSaveFails_LeavesNoPartialMembership()
    {
        await using var host = await ClinicManagementTestHost.CreateAsync(services =>
            services.UseFailingClinicSaveInterceptor());

        var interceptor = host.GetRequiredService<FailingClinicSaveInterceptor>();
        var seed = await ClinicManagementTestHostSeeder.SeedClinicWithManagerAsync(host);
        var member = await ClinicManagementTestHostSeeder.SeedDoctorAsync(
            host,
            name: "Dr. Rollback Member",
            phoneNumber: "+15552000444");

        var beforeLinks = await host.DbContext.ClinicDoctors.CountAsync();
        interceptor.FailOnNextClinicRelatedSave = true;

        var controller = host.CreateAdminController(seed.Admin.Id);
        var act = async () => await controller.AddClinicDoctor(
            seed.ClinicId,
            member.Id,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated clinic persistence failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(beforeLinks);
        (await host.DbContext.ClinicDoctors.AnyAsync(link => link.DoctorId == member.Id))
            .Should().BeFalse();
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
