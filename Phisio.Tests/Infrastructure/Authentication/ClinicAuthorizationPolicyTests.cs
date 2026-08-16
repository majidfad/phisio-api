using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Application.Common;

namespace Phisio.Tests.Infrastructure.Authentication;

public class ClinicAuthorizationPolicyTests
{
    [Theory]
    [InlineData(RoleNames.Admin, true)]
    [InlineData(RoleNames.ClinicManager, true)]
    [InlineData(RoleNames.Doctor, true)]
    [InlineData(RoleNames.Patient, false)]
    public async Task ClinicManagementPolicy_AllowsAdminClinicManagerAndDoctor(
        string role,
        bool shouldAuthorize)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ClinicManagement, policy =>
                policy.RequireRole(RoleNames.Admin, RoleNames.ClinicManager, RoleNames.Doctor));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"));

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            AuthorizationPolicies.ClinicManagement);

        result.Succeeded.Should().Be(shouldAuthorize);
    }
}
