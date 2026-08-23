using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ClinicServiceAssignDoctorTests
{
    [Fact]
    public async Task LookupByPhonesAsync_WhenNoClinicMatches_ReturnsNone()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        context.Users.Add(admin);

        var sut = new ClinicService(context);
        var result = await sut.LookupByPhonesAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new LookupClinicsByPhonesDto { PhoneNumbers = ["021-12345678"] });

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(ClinicPhoneLookupStatus.None);
        result.Value.Clinic.Should().BeNull();
    }

    [Fact]
    public async Task LookupByPhonesAsync_WhenPhoneMatchesOneClinic_ReturnsFound()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        context.Users.AddRange(admin, manager);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Vanak Clinic");
        clinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "021-12345678",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("021-12345678"),
        });
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.LookupByPhonesAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new LookupClinicsByPhonesDto { PhoneNumbers = ["02112345678"] });

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(ClinicPhoneLookupStatus.Found);
        result.Value.Clinic!.ClinicId.Should().Be(clinic.ClinicId);
        result.Value.Clinic.Name.Should().Be("Vanak Clinic");
    }

    [Fact]
    public async Task LookupByPhonesAsync_WhenPhonesBelongToDifferentClinics_ReturnsConflict()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var firstManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var secondManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        context.Users.AddRange(admin, firstManager, secondManager);

        var firstClinic = ClinicServiceTestHelper.CreateClinic(firstManager.Id, "Clinic A");
        firstClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02111111111",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02111111111"),
        });
        var secondClinic = ClinicServiceTestHelper.CreateClinic(secondManager.Id, "Clinic B");
        secondClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02122222222",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02122222222"),
        });
        context.Clinics.AddRange(firstClinic, secondClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.LookupByPhonesAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new LookupClinicsByPhonesDto
            {
                PhoneNumbers = ["02111111111", "02122222222"],
            });

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(ClinicPhoneLookupStatus.Conflict);
        result.Value.ConflictingClinics.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenClinicExists_AddsDoctorWithoutCreatingClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(admin, manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Existing Clinic");
        clinic.EnsureManagerDoctorMembership();
        clinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02112345678",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02112345678"),
        });
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["021-12345678"],
            });

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicCreated.Should().BeFalse();
        result.Value.Clinic.ClinicId.Should().Be(clinic.ClinicId);
        result.Value.Doctor.DoctorId.Should().Be(doctor.Id);

        (await context.ClinicDoctors.CountAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == doctor.Id)).Should().Be(1);
        (await context.Clinics.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenPendingDoctorIsAllowed_AddsDoctorToClinic()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        doctor.IsEnabled = false;
        context.Users.AddRange(admin, manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Existing Clinic");
        clinic.EnsureManagerDoctorMembership();
        clinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02112345678",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02112345678"),
        });
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02112345678"],
                AllowDisabledDoctor = true,
            });

        result.Succeeded.Should().BeTrue();
        (await context.ClinicDoctors.CountAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == doctor.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenDoctorAlreadyAssigned_DoesNotDuplicateMembership()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var manager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(admin, manager, doctor);

        var clinic = ClinicServiceTestHelper.CreateClinic(manager.Id, "Existing Clinic");
        clinic.EnsureManagerDoctorMembership();
        clinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02112345678",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02112345678"),
        });
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02112345678"],
            });

        result.Succeeded.Should().BeTrue();
        (await context.ClinicDoctors.CountAsync(link =>
            link.ClinicId == clinic.ClinicId && link.DoctorId == doctor.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenPhonesConflict_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var firstManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15551111111");
        var secondManager = ApplicationUserBuilder.ClinicManager(phoneNumber: "+15552222222");
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(admin, firstManager, secondManager, doctor);

        var firstClinic = ClinicServiceTestHelper.CreateClinic(firstManager.Id, "Clinic A");
        firstClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02111111111",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02111111111"),
        });
        var secondClinic = ClinicServiceTestHelper.CreateClinic(secondManager.Id, "Clinic B");
        secondClinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = "02122222222",
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize("02122222222"),
        });
        context.Clinics.AddRange(firstClinic, secondClinic);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02111111111", "02122222222"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.ConflictingClinicPhones);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenNoClinicFoundWithoutCreateDetails_ReturnsFailure()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(admin, doctor);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02199999999"],
            });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.ClinicCreateDetailsRequired);
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenNoClinicFound_CreatesClinicWithDoctorAsManager()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        var clinicManagerRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = RoleNames.ClinicManager,
            NormalizedName = RoleNames.ClinicManager.ToUpperInvariant(),
        };
        context.Users.AddRange(admin, doctor);
        context.Roles.Add(clinicManagerRole);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02188888888"],
                Name = "New Clinic",
                Address = "New Address",
                ManagerIsThisDoctor = true,
            });

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicCreated.Should().BeTrue();
        result.Value.Clinic.ClinicManagerId.Should().Be(doctor.Id);
        result.Value.Doctor.DoctorId.Should().Be(doctor.Id);

        var clinic = await context.Clinics
            .Include(item => item.ClinicDoctors)
            .Include(item => item.PhoneNumbers)
            .SingleAsync();
        clinic.HasManagerDoctorMembership().Should().BeTrue();
        clinic.PhoneNumbers.Should().ContainSingle();
    }

    [Fact]
    public async Task AssignDoctorAsync_WhenNoClinicFound_CreatesClinicWithSelectedManagerAndAddsDoctor()
    {
        await using var context = ClinicServiceTestHelper.CreateContext();
        var admin = ApplicationUserBuilder.Admin();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        var manager = ApplicationUserBuilder.Doctor(name: "Manager Doctor", phoneNumber: "+15554444444");
        var clinicManagerRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = RoleNames.ClinicManager,
            NormalizedName = RoleNames.ClinicManager.ToUpperInvariant(),
        };
        context.Users.AddRange(admin, doctor, manager);
        context.Roles.Add(clinicManagerRole);
        await context.SaveChangesAsync();

        var sut = new ClinicService(context);
        var result = await sut.AssignDoctorAsync(
            new ClinicAccessContext(admin.Id, IsAdmin: true),
            new AssignDoctorToClinicDto
            {
                DoctorId = doctor.Id,
                PhoneNumbers = ["02177777777"],
                Name = "Managed Clinic",
                Address = "Managed Address",
                ManagerIsThisDoctor = false,
                ClinicManagerId = manager.Id,
            });

        result.Succeeded.Should().BeTrue();
        result.Value!.Clinic.ClinicManagerId.Should().Be(manager.Id);

        var doctorIds = await context.ClinicDoctors
            .Where(link => link.ClinicId == result.Value.Clinic.ClinicId)
            .Select(link => link.DoctorId)
            .ToListAsync();
        doctorIds.Should().BeEquivalentTo([manager.Id, doctor.Id]);
    }
}
