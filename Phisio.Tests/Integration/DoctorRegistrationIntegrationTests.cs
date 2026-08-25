using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phisio.Api.Controllers;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Auth;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class DoctorRegistrationIntegrationTests
{
    [Fact]
    public async Task RegisterDoctor_WhenClinicPhoneExists_LinksDoctorToExistingClinic()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var clinicPhone = "02112345678";
        var existingClinicId = await SeedExistingClinicAsync(host, "Vanak Clinic", "Vanak Address", clinicPhone);

        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110001",
            clinicPhones: [clinicPhone]);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<RegisterResponse>().Subject;
        body.Role.Should().Be(UserRole.Doctor);

        var doctor = await FindDoctorByPhoneAsync(host, request.PhoneNumber);
        doctor.Should().NotBeNull();
        doctor!.IsEnabled.Should().BeFalse();

        (await host.DbContext.Clinics.CountAsync()).Should().Be(1);
        var clinic = await host.DbContext.Clinics.SingleAsync();
        clinic.ClinicId.Should().Be(existingClinicId);

        var membership = await host.DbContext.ClinicDoctors.SingleAsync(link =>
            link.DoctorId == doctor.Id);
        membership.ClinicId.Should().Be(existingClinicId);
    }

    [Fact]
    public async Task RegisterDoctor_WhenClinicPhoneDoesNotExist_CreatesClinicAndMembership()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var clinicPhone = "02188880001";
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110002",
            clinicPhones: [clinicPhone],
            newClinicName: "New Registration Clinic",
            newClinicAddress: "New Clinic Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<RegisterResponse>().Subject;

        var doctor = await FindDoctorByPhoneAsync(host, request.PhoneNumber);
        doctor.Should().NotBeNull();
        doctor!.Id.Should().Be(body.UserId);

        var clinic = await host.DbContext.Clinics
            .Include(item => item.PhoneNumbers)
            .Include(item => item.ClinicDoctors)
            .SingleAsync();

        clinic.Name.Should().Be("New Registration Clinic");
        clinic.Address.Should().Be("New Clinic Address");
        clinic.ClinicManagerId.Should().Be(doctor.Id);
        clinic.PhoneNumbers.Should().ContainSingle(phone =>
            phone.NormalizedPhoneNumber == PhoneNumberNormalizer.Normalize(clinicPhone));
        clinic.ClinicDoctors.Should().ContainSingle(link => link.DoctorId == doctor.Id);
    }

    [Fact]
    public async Task RegisterDoctor_WhenMultipleClinicPhonesProvided_SavesAllPhonesOnNewClinic()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var phones = new[] { "02177770001", "02177770002", "09127770003" };
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110003",
            clinicPhones: phones,
            newClinicName: "Multi Phone Clinic",
            newClinicAddress: "Multi Phone Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        var clinic = await host.DbContext.Clinics
            .Include(item => item.PhoneNumbers)
            .SingleAsync();

        clinic.PhoneNumbers.Select(phone => phone.NormalizedPhoneNumber)
            .Should()
            .BeEquivalentTo(phones.Select(PhoneNumberNormalizer.Normalize));
    }

    [Fact]
    public async Task RegisterDoctor_WhenManagerIsThisDoctor_SetsDoctorAsClinicManager()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110004",
            clinicPhones: ["02166660001"],
            newClinicName: "Managed By Doctor Clinic",
            newClinicAddress: "Manager Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = created.Value.Should().BeOfType<RegisterResponse>().Subject;

        var doctor = await FindDoctorByPhoneAsync(host, request.PhoneNumber);
        doctor.Should().NotBeNull();

        var clinic = await host.DbContext.Clinics
            .Include(item => item.ClinicDoctors)
            .SingleAsync();

        clinic.ClinicManagerId.Should().Be(doctor!.Id);
        clinic.ClinicDoctors.Should().Contain(link => link.DoctorId == doctor.Id);

        var clinicManagerRoleId = await host.DbContext.Roles
            .Where(role => role.Name == RoleNames.ClinicManager)
            .Select(role => role.Id)
            .SingleAsync();

        (await host.DbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == doctor.Id && userRole.RoleId == clinicManagerRoleId))
            .Should()
            .BeTrue();

        body.UserId.Should().Be(doctor.Id);
    }

    [Fact]
    public async Task RegisterDoctor_WhenCreatingClinicWithoutName_ReturnsValidationError()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110005",
            clinicPhones: ["02155550001"],
            newClinicName: null,
            newClinicAddress: "Address Only",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ClinicCreateDetailsRequired);

        await AssertNoDoctorOrClinicCreatedAsync(host, request.PhoneNumber);
    }

    [Fact]
    public async Task RegisterDoctor_WhenCreatingClinicWithoutAddress_ReturnsValidationError()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110006",
            clinicPhones: ["02155550002"],
            newClinicName: "Name Only",
            newClinicAddress: null,
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ClinicCreateDetailsRequired);

        await AssertNoDoctorOrClinicCreatedAsync(host, request.PhoneNumber);
    }

    [Fact]
    public async Task RegisterDoctor_WhenPhoneBelongsToExistingClinic_ReusesClinicWithoutDuplicate()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var clinicPhone = "02144440001";
        var existingClinicId = await SeedExistingClinicAsync(
            host,
            "Reusable Clinic",
            "Reusable Address",
            clinicPhone);

        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110007",
            clinicPhones: [clinicPhone],
            newClinicName: "Should Not Be Created",
            newClinicAddress: "Should Not Be Used",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        var doctor = await FindDoctorByPhoneAsync(host, request.PhoneNumber);
        doctor.Should().NotBeNull();

        (await host.DbContext.Clinics.CountAsync()).Should().Be(1);
        var clinic = await host.DbContext.Clinics.SingleAsync();
        clinic.ClinicId.Should().Be(existingClinicId);
        clinic.Name.Should().Be("Reusable Clinic");

        (await host.DbContext.ClinicDoctors.CountAsync(link =>
            link.ClinicId == existingClinicId && link.DoctorId == doctor!.Id))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task RegisterDoctor_WhenClinicPhonesConflictAcrossClinics_ReturnsValidationError()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        await SeedExistingClinicAsync(host, "Clinic A", "Address A", "02133330001");
        await SeedExistingClinicAsync(host, "Clinic B", "Address B", "02133330002");

        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110008",
            clinicPhones: ["02133330001", "02133330002"],
            newClinicName: "Conflict Clinic",
            newClinicAddress: "Conflict Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(ClinicErrors.ConflictingClinicPhones);

        await AssertNoDoctorCreatedAsync(host, request.PhoneNumber);
        (await host.DbContext.Clinics.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task RegisterDoctor_WhenDoctorPhoneAlreadyExists_RejectsRegistration()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        await SeedExistingDoctorAsync(host, "09121110009");

        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110009",
            clinicPhones: ["02122220001"],
            newClinicName: "Duplicate Doctor Clinic",
            newClinicAddress: "Duplicate Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(AuthErrorMessages.DuplicatePhoneNumber);

        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(request.PhoneNumber)))
            .Should()
            .Be(1);
        (await host.DbContext.Clinics.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterDoctor_WhenClinicAssignmentFails_RollsBackDoctorCreation()
    {
        await using var host = await RegistrationTestHost.CreateAsync(services =>
        {
            services.RemoveAll<IClinicService>();
            services.AddScoped<ClinicService>();
            services.AddScoped<IClinicService>(provider =>
                new AssignFailingClinicService(provider.GetRequiredService<ClinicService>()));
        });

        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110010",
            clinicPhones: ["02111110001"],
            newClinicName: "Rollback Clinic",
            newClinicAddress: "Rollback Address",
            managerIsThisDoctor: true);

        var result = await host.AuthController.Register(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain("Simulated clinic assignment failure.");

        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(request.PhoneNumber)))
            .Should()
            .Be(0);
        (await host.DbContext.DoctorProfiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterDoctor_PublicEndpoint_AllowsAnonymousAccess()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreateDoctorRegisterRequest(
            phoneNumber: "09121110011",
            clinicPhones: ["02100000011"],
            newClinicName: "Public Clinic",
            newClinicAddress: "Public Address",
            managerIsThisDoctor: true);

        typeof(AuthController)
            .GetMethod(nameof(AuthController.Register))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
            .Should()
            .NotBeNull();

        var result = await host.AuthController.Register(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task AdminOnlyEndpoints_RejectAnonymousUsers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var adminPolicyResult = await authorizationService.AuthorizeAsync(
            anonymous,
            resource: null,
            AuthorizationPolicies.AdminOnly);

        adminPolicyResult.Succeeded.Should().BeFalse();

        typeof(AdminDoctorsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!
            .Policy
            .Should()
            .Be(AuthorizationPolicies.AdminOnly);
    }

    private static async Task<Guid> SeedExistingClinicAsync(
        RegistrationTestHost host,
        string name,
        string address,
        string phoneNumber)
    {
        var manager = ApplicationUserBuilder.ClinicManager(
            phoneNumber: $"+1555{Random.Shared.Next(1000000, 9999999)}");
        host.DbContext.Users.Add(manager);

        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            Address = address,
            ClinicManagerId = manager.Id,
        };
        clinic.EnsureManagerDoctorMembership();
        clinic.PhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber),
        });

        host.DbContext.Clinics.Add(clinic);
        await host.DbContext.SaveChangesAsync();
        return clinic.ClinicId;
    }

    private static async Task SeedExistingDoctorAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        var doctor = ApplicationUserBuilder.Doctor(
            phoneNumber: PhoneNumberNormalizer.Normalize(phoneNumber));
        doctor.IsEnabled = false;
        host.DbContext.Users.Add(doctor);

        var doctorRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Doctor);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = doctor.Id,
            RoleId = doctorRole.Id,
        });

        host.DbContext.DoctorProfiles.Add(new DoctorProfile
        {
            DoctorProfileId = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Specialty = "Physiotherapy",
            MedicalLicenseNumber = $"LIC-{Guid.NewGuid():N}"[..16],
            ClinicAddress = "Existing Address",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
        });

        await host.DbContext.SaveChangesAsync();
    }

    private static async Task AssertNoDoctorOrClinicCreatedAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        await AssertNoDoctorCreatedAsync(host, phoneNumber);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static async Task AssertNoDoctorCreatedAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(phoneNumber)))
            .Should()
            .Be(0);
        (await host.DbContext.DoctorProfiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static async Task<ApplicationUser?> FindDoctorByPhoneAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        return await host.DbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user =>
                user.PhoneNumber == normalized && user.Role == UserRole.Doctor);
    }

    private static RegisterRequest CreateDoctorRegisterRequest(
        string phoneNumber,
        IList<string> clinicPhones,
        string? newClinicName = null,
        string? newClinicAddress = null,
        bool managerIsThisDoctor = false) =>
        new()
        {
            Name = "دکتر تست ثبت‌نام",
            PhoneNumber = phoneNumber,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = UserRole.Doctor,
            MedicalLicenseNumber = $"ML-{Guid.NewGuid():N}"[..12],
            Specialty = "فیزیوتراپی",
            ClinicPhoneNumbers = clinicPhones,
            NewClinicName = newClinicName,
            NewClinicAddress = newClinicAddress,
            ManagerIsThisDoctor = managerIsThisDoctor,
        };

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
