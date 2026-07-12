using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorDashboardServiceGetDashboardTests
{
    [Fact]
    public async Task GetDashboardAsync_ReturnsPatientCountAndRecentPatientsForDoctor()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15559999999");
        var alice = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var bob = ApplicationUserBuilder.Patient(name: "Bob Patient", phoneNumber: "+15552222222");
        var charlie = ApplicationUserBuilder.Patient(name: "Charlie Patient", phoneNumber: "+15553333333");

        var doctorPatients = new[]
        {
            DoctorPatientBuilder.Create(doctor.Id, alice.Id, DateTime.UtcNow.AddDays(-1)),
            DoctorPatientBuilder.Create(doctor.Id, bob.Id, DateTime.UtcNow.AddDays(-2)),
            DoctorPatientBuilder.Create(doctor.Id, charlie.Id, DateTime.UtcNow.AddDays(-3)),
            DoctorPatientBuilder.Create(otherDoctor.Id, alice.Id, DateTime.UtcNow.AddDays(-1), isEnabled: false),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, otherDoctor, alice, bob, charlie],
            doctorPatients: doctorPatients);

        var sut = new DoctorDashboardService(dbContext.Object);

        // Act
        var result = await sut.GetDashboardAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PatientsCount.Should().Be(3);
        result.Value.RecentPatients.Should().HaveCount(3);
        result.Value.RecentPatients.Select(patient => patient.PatientName)
            .Should().ContainInOrder("Alice Patient", "Bob Patient", "Charlie Patient");
    }

    [Fact]
    public async Task GetDashboardAsync_WhenDoctorHasNoData_ReturnsZeroPatientsAndEmptyRecentPatients()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var sut = new DoctorDashboardService(AppDbContextMockFactory.CreateMock(users: [doctor]).Object);

        // Act
        var result = await sut.GetDashboardAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PatientsCount.Should().Be(0);
        result.Value.RecentPatients.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsAtMostFiveRecentPatients()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patients = Enumerable.Range(0, 6)
            .Select(index => ApplicationUserBuilder.Patient(
                name: $"Patient {index}",
                phoneNumber: $"+155500000{index:00}"))
            .ToArray();

        var doctorPatients = patients
            .Select((patient, index) => DoctorPatientBuilder.Create(
                doctor.Id,
                patient.Id,
                DateTime.UtcNow.AddDays(-index)))
            .ToArray();

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, ..patients],
            doctorPatients: doctorPatients);

        var sut = new DoctorDashboardService(dbContext.Object);

        // Act
        var result = await sut.GetDashboardAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.RecentPatients.Should().HaveCount(5);
        result.Value.RecentPatients.First().PatientName.Should().Be("Patient 0");
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesSoftDeletedRelationshipsForDoctor()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var activePatient = ApplicationUserBuilder.Patient(name: "Active Patient");
        var removedPatient = ApplicationUserBuilder.Patient(name: "Removed Patient", phoneNumber: "+15552222222");

        var doctorPatients = new[]
        {
            DoctorPatientBuilder.Create(doctor.Id, activePatient.Id, DateTime.UtcNow.AddDays(-1)),
            DoctorPatientBuilder.Create(doctor.Id, removedPatient.Id, DateTime.UtcNow.AddDays(-2), isEnabled: false),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, activePatient, removedPatient],
            doctorPatients: doctorPatients);

        var sut = new DoctorDashboardService(dbContext.Object);

        // Act
        var result = await sut.GetDashboardAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.PatientsCount.Should().Be(1);
        result.Value.RecentPatients.Should().ContainSingle()
            .Which.PatientName.Should().Be("Active Patient");
    }
}
