using FluentAssertions;
using Phisio.Application.Common;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Application.Common;

public class UserRoleExtensionsTests
{
    [Theory]
    [InlineData(UserRole.Doctor, true)]
    [InlineData(UserRole.ClinicManager, true)]
    [InlineData(UserRole.Patient, false)]
    [InlineData(UserRole.Admin, false)]
    public void HasDoctorAccess_ReturnsExpectedResult(UserRole role, bool expected)
    {
        role.HasDoctorAccess().Should().Be(expected);
    }

    [Fact]
    public void RoleNames_DoctorAccess_IncludesDoctorAndClinicManagerOnly()
    {
        RoleNames.DoctorAccess.Should().BeEquivalentTo(
            [RoleNames.Doctor, RoleNames.ClinicManager]);
    }
}
