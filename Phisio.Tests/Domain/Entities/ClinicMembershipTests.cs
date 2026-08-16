using FluentAssertions;
using Phisio.Domain.Entities;

namespace Phisio.Tests.Clinics;

public class ClinicMembershipTests
{
    [Fact]
    public void EnsureManagerDoctorMembership_WhenMissing_AddsManagerAsDoctor()
    {
        var clinicId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var clinic = new Clinic
        {
            ClinicId = clinicId,
            ClinicManagerId = managerId,
            Name = "Test Clinic",
            Address = "Test Address",
        };

        clinic.EnsureManagerDoctorMembership();

        clinic.HasManagerDoctorMembership().Should().BeTrue();
        clinic.ClinicDoctors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ClinicDoctor
            {
                ClinicId = clinicId,
                DoctorId = managerId,
            }, options => options.Excluding(link => link.Clinic));
    }

    [Fact]
    public void EnsureManagerDoctorMembership_WhenAlreadyPresent_IsIdempotent()
    {
        var clinicId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var clinic = new Clinic
        {
            ClinicId = clinicId,
            ClinicManagerId = managerId,
            Name = "Test Clinic",
            Address = "Test Address",
        };

        clinic.EnsureManagerDoctorMembership();
        clinic.EnsureManagerDoctorMembership();

        clinic.ClinicDoctors.Should().ContainSingle()
            .Which.DoctorId.Should().Be(managerId);
    }

    [Fact]
    public void EnsureManagerDoctorMembership_DoesNotRemoveOtherDoctors()
    {
        var clinicId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var clinic = new Clinic
        {
            ClinicId = clinicId,
            ClinicManagerId = managerId,
            Name = "Test Clinic",
            Address = "Test Address",
            ClinicDoctors =
            [
                new ClinicDoctor { ClinicId = clinicId, DoctorId = otherDoctorId },
            ],
        };

        clinic.EnsureManagerDoctorMembership();

        clinic.ClinicDoctors.Select(link => link.DoctorId)
            .Should().BeEquivalentTo([managerId, otherDoctorId]);
    }
}
