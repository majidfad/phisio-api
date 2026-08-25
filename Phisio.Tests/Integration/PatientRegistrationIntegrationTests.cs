using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers;
using Phisio.Api.Controllers.Admin;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Auth;
using Phisio.Application.Common;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class PatientRegistrationIntegrationTests
{
    [Fact]
    public async Task RegisterPatient_WhenRequestIsValid_CreatesPatientUserAndRole()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000001");

        var result = await host.RegisterPatientValidatedAsync(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<RegisterPatientResponse>().Subject;
        body.Role.Should().Be(UserRole.Patient);
        body.Name.Should().Be(request.Name);
        body.PhoneNumber.Should().Be(PhoneNumberNormalizer.Normalize(request.PhoneNumber));

        var patient = await FindPatientByPhoneAsync(host, request.PhoneNumber);
        patient.Should().NotBeNull();
        patient!.Id.Should().Be(body.UserId);
        patient.Role.Should().Be(UserRole.Patient);
        patient.IsEnabled.Should().BeTrue();
        patient.Name.Should().Be(request.Name);

        // Patients have no separate profile table; the ApplicationUser record is the patient identity.
        (await host.DbContext.DoctorProfiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);

        var patientRoleId = await host.DbContext.Roles
            .Where(role => role.Name == RoleNames.Patient)
            .Select(role => role.Id)
            .SingleAsync();
        (await host.DbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == patient.Id && userRole.RoleId == patientRoleId))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Register_WhenRoleIsPatient_CreatesPatientThroughPublicRegisterEndpoint()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreateRegisterAsPatientRequest(phoneNumber: "09130000002");

        var result = await host.RegisterValidatedAsync(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<RegisterResponse>().Subject;
        body.Role.Should().Be(UserRole.Patient);
        body.Message.Should().Be(RegisterMessages.PatientRegistered);

        var patient = await FindPatientByPhoneAsync(host, request.PhoneNumber);
        patient.Should().NotBeNull();
        patient!.Role.Should().Be(UserRole.Patient);
        patient.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterPatient_WhenPhoneAlreadyExists_RejectsRegistration()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        await SeedExistingPatientAsync(host, "09130000003");

        var request = CreatePatientRequest(phoneNumber: "09130000003");
        var result = await host.RegisterPatientValidatedAsync(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain(AuthErrorMessages.DuplicatePhoneNumber);

        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(request.PhoneNumber)))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task RegisterPatient_WhenRequiredFieldsMissing_ReturnsValidationErrors()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = new RegisterPatientRequest
        {
            Name = string.Empty,
            PhoneNumber = string.Empty,
            Password = string.Empty,
        };

        var result = await host.RegisterPatientValidatedAsync(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().NotBeEmpty();

        (await host.DbContext.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterPatient_WhenPhoneNumberIsInvalid_ReturnsValidationError()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "not-a-phone");

        var result = await host.RegisterPatientValidatedAsync(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain("Phone number format is invalid.");
        (await host.DbContext.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterPatient_WhenPasswordIsInvalid_ReturnsValidationErrors()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000004", password: "weak");

        var result = await host.RegisterPatientValidatedAsync(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().NotBeEmpty();
        (await host.DbContext.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterPatient_WithoutDoctor_SucceedsWithNoDoctorPatientLinks()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000005");

        var result = await host.RegisterPatientValidatedAsync(request);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        var patient = await FindPatientByPhoneAsync(host, request.PhoneNumber);
        patient.Should().NotBeNull();

        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(0);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterPatient_DoesNotCreateClinicOrDoctorRelationships()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000006");

        var result = await host.RegisterPatientValidatedAsync(request);

        result.Should().BeOfType<CreatedAtActionResult>();

        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(0);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicPhoneNumbers.CountAsync()).Should().Be(0);
        (await host.DbContext.DoctorProfiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user => user.Role == UserRole.Doctor))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task RegisterPatient_WhenRegisteredTwice_SecondAttemptFailsWithoutDuplicates()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000007");

        var first = await host.RegisterPatientValidatedAsync(request);
        first.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        var second = await host.RegisterPatientValidatedAsync(request);
        var badRequest = second.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractErrors(badRequest.Value).Should().Contain(AuthErrorMessages.DuplicatePhoneNumber);

        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(request.PhoneNumber)))
            .Should()
            .Be(1);
        (await host.DbContext.UserRoles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Login_AfterSuccessfulPatientRegistration_ReturnsJwtWithPatientRole()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000008");

        var registerResult = await host.RegisterPatientValidatedAsync(request);
        registerResult.Should().BeOfType<CreatedAtActionResult>();

        var loginResult = await host.AuthController.Login(
            new LoginRequest
            {
                PhoneNumber = request.PhoneNumber,
                Password = request.Password,
            },
            CancellationToken.None);

        var ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var auth = ok.Value.Should().BeOfType<AuthResponse>().Subject;

        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Role.Should().Be(UserRole.Patient);
        auth.UserId.Should().NotBeEmpty();
        auth.PhoneNumber.Should().Be(PhoneNumberNormalizer.Normalize(request.PhoneNumber));
        auth.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task RegisterPatient_PublicEndpoint_AllowsAnonymousAccess()
    {
        typeof(AuthController)
            .GetMethod(nameof(AuthController.RegisterPatient))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
            .Should()
            .NotBeNull();

        typeof(AuthController)
            .GetMethod(nameof(AuthController.Register))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
            .Should()
            .NotBeNull();

        await using var host = await RegistrationTestHost.CreateAsync();
        var result = await host.RegisterPatientValidatedAsync(
            CreatePatientRequest(phoneNumber: "09130000009"));

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task Authorization_AdminAndDoctorEndpoints_RejectPatientAndAnonymousUsers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.DoctorAccess));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var patient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Patient)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();

        typeof(AdminDoctorsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!
            .Policy
            .Should()
            .Be(AuthorizationPolicies.AdminOnly);

        typeof(DoctorPatientsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!
            .Policy
            .Should()
            .Be(AuthorizationPolicies.DoctorOnly);
    }

    [Fact]
    public async Task RegisterPatient_DatabaseState_IsConsistentWithPatientOnlyIdentity()
    {
        await using var host = await RegistrationTestHost.CreateAsync();
        var request = CreatePatientRequest(phoneNumber: "09130000010");

        var result = await host.RegisterPatientValidatedAsync(request);
        var body = result.Should().BeOfType<CreatedAtActionResult>().Subject
            .Value.Should().BeOfType<RegisterPatientResponse>().Subject;

        var users = await host.DbContext.Users.IgnoreQueryFilters().ToListAsync();
        users.Should().ContainSingle();
        users[0].Id.Should().Be(body.UserId);
        users[0].Role.Should().Be(UserRole.Patient);

        var roles = await host.DbContext.UserRoles.ToListAsync();
        roles.Should().ContainSingle();

        (await host.DbContext.DoctorProfiles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicDoctors.CountAsync()).Should().Be(0);
        (await host.DbContext.ClinicPhoneNumbers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RegisterPatient_WhenRoleAssignmentFails_RollsBackCreatedUser()
    {
        await using var host = await RegistrationTestHost.CreateAsync(services =>
            services.UseFailingAddToRoleUserManager());

        var request = CreatePatientRequest(phoneNumber: "09130000011");
        var result = await host.RegisterPatientValidatedAsync(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ExtractErrors(badRequest.Value).Should().Contain("Simulated role assignment failure.");

        (await host.DbContext.Users.IgnoreQueryFilters()
            .CountAsync(user =>
                user.PhoneNumber == PhoneNumberNormalizer.Normalize(request.PhoneNumber)))
            .Should()
            .Be(0);
        (await host.DbContext.UserRoles.CountAsync()).Should().Be(0);
        (await host.DbContext.DoctorPatients.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.Clinics.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static async Task SeedExistingPatientAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        var patient = ApplicationUserBuilder.Patient(
            phoneNumber: PhoneNumberNormalizer.Normalize(phoneNumber));
        host.DbContext.Users.Add(patient);

        var patientRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Patient);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = patient.Id,
            RoleId = patientRole.Id,
        });

        await host.DbContext.SaveChangesAsync();
    }

    private static async Task<ApplicationUser?> FindPatientByPhoneAsync(
        RegistrationTestHost host,
        string phoneNumber)
    {
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        return await host.DbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user =>
                user.PhoneNumber == normalized && user.Role == UserRole.Patient);
    }

    private static RegisterPatientRequest CreatePatientRequest(
        string phoneNumber,
        string password = "Password123!") =>
        new()
        {
            Name = "بیمار تست",
            PhoneNumber = phoneNumber,
            Password = password,
        };

    private static RegisterRequest CreateRegisterAsPatientRequest(string phoneNumber) =>
        new()
        {
            Name = "بیمار تست عمومی",
            PhoneNumber = phoneNumber,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = UserRole.Patient,
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
