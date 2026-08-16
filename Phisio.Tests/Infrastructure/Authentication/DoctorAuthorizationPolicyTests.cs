using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Application.Common;

namespace Phisio.Tests.Infrastructure.Authentication;

public class DoctorAuthorizationPolicyTests
{
    [Theory]
    [InlineData(RoleNames.Doctor, true)]
    [InlineData(RoleNames.ClinicManager, true)]
    [InlineData(RoleNames.Patient, false)]
    [InlineData(RoleNames.Admin, false)]
    public async Task DoctorOnlyPolicy_AllowsDoctorCapableRolesOnly(string role, bool shouldAuthorize)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.DoctorAccess));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"));

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            AuthorizationPolicies.DoctorOnly);

        result.Succeeded.Should().Be(shouldAuthorize);
    }

    [Fact]
    public async Task DoctorOnlyPolicy_WhenUserHasBothDoctorRoles_AllowsAccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.DoctorAccess));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, RoleNames.Doctor),
                new Claim(ClaimTypes.Role, RoleNames.ClinicManager),
            ],
            authenticationType: "Test"));

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            AuthorizationPolicies.DoctorOnly);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task PatientOnlyPolicy_StillDeniesClinicManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.PatientOnly, policy =>
                policy.RequireRole(RoleNames.Patient));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.ClinicManager)],
            authenticationType: "Test"));

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            AuthorizationPolicies.PatientOnly);

        result.Succeeded.Should().BeFalse();
    }
}
