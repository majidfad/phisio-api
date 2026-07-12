using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceGetPatientsTests
{
    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasNoPatients_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasPatients_ReturnsActiveRelationshipsOnly()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var activePatient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var inactivePatient = ApplicationUserBuilder.Patient(name: "Bob Patient", phoneNumber: "+15552222222");
        var assignedAt = DateTime.UtcNow.AddDays(-3);

        var doctorPatients = new[]
        {
            DoctorPatientBuilder.Create(doctor.Id, activePatient.Id, assignedAt),
            DoctorPatientBuilder.Create(doctor.Id, inactivePatient.Id, isEnabled: false),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, activePatient, inactivePatient],
            doctorPatients: doctorPatients);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().PatientId.Should().Be(activePatient.Id);
        result.Value.Single().PatientName.Should().Be(activePatient.Name);
        result.Value.Single().PhoneNumber.Should().Be(activePatient.PhoneNumber);
        result.Value.Single().AssignedAt.Should().BeCloseTo(assignedAt, TimeSpan.FromSeconds(1));
    }
}

public class DoctorPatientServiceAddByPhoneTests
{
    [Fact]
    public async Task AddByPhoneAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AddByPhoneAsync(doctor.Id, new AddDoctorPatientRequest("+19999999999"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.PatientNotFoundError);
    }

    [Fact]
    public async Task AddByPhoneAsync_WhenPatientExists_CreatesRelationship()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AddByPhoneAsync(doctor.Id, new AddDoctorPatientRequest("+15551111111"));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.PatientId.Should().Be(patient.Id);
        dbContext.Object.DoctorPatients.Should().ContainSingle();
    }

    [Fact]
    public async Task AddByPhoneAsync_WhenRelationshipAlreadyActive_ReturnsDuplicateFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AddByPhoneAsync(doctor.Id, new AddDoctorPatientRequest("+15551111111"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.DuplicateAssignmentError);
    }

    [Fact]
    public async Task AddByPhoneAsync_WhenRelationshipWasSoftDeleted_RestoresRelationship()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.AddByPhoneAsync(doctor.Id, new AddDoctorPatientRequest("+15551111111"));

        // Assert
        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeTrue();
    }
}

public class DoctorPatientServiceRemoveTests
{
    [Fact]
    public async Task RemoveAsync_WhenRelationshipExists_SoftDeletesRelationship()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.RemoveAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.Should().BeEmpty();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_WhenRelationshipNotFound_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.RemoveAsync(doctor.Id, Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.RelationshipNotFoundError);
    }

    [Fact]
    public async Task RemoveAsync_WhenRelationshipAlreadySoftDeleted_ReturnsNotFound()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.RemoveAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.RelationshipNotFoundError);
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }
}
