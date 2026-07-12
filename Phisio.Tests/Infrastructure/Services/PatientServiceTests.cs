using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientServiceGetPatientsTests
{
    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasNoPatients_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasOnePatient_ReturnsSinglePatientDto()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var assignedAt = DateTime.UtcNow.AddDays(-5);
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id, assignedAt);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().ContainSingle();

        var patientDto = result.Value.Single();
        patientDto.Id.Should().Be(patient.Id);
        patientDto.Name.Should().Be(patient.Name);
        patientDto.PhoneNumber.Should().Be(patient.PhoneNumber);
        patientDto.FirstAssignedAt.Should().BeCloseTo(assignedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasMultiplePatients_ReturnsPatientsOrderedByName()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var charlie = ApplicationUserBuilder.Patient(name: "Charlie Patient", phoneNumber: "+15553333333");
        var alice = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var bob = ApplicationUserBuilder.Patient(name: "Bob Patient", phoneNumber: "+15552222222");

        var doctorPatients = new[]
        {
            DoctorPatientBuilder.Create(doctor.Id, charlie.Id, DateTime.UtcNow.AddDays(-1)),
            DoctorPatientBuilder.Create(doctor.Id, alice.Id, DateTime.UtcNow.AddDays(-3)),
            DoctorPatientBuilder.Create(doctor.Id, bob.Id, DateTime.UtcNow.AddDays(-2)),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, charlie, alice, bob],
            doctorPatients: doctorPatients);

        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(3);
        result.Value.Select(dto => dto.Name).Should().ContainInOrder("Alice Patient", "Bob Patient", "Charlie Patient");
    }

    [Fact]
    public async Task GetPatientsAsync_WhenDoctorIdIsInvalid_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientsAsync_ExcludesSoftDeletedRelationships()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var activePatient = ApplicationUserBuilder.Patient(name: "Active Patient");
        var removedPatient = ApplicationUserBuilder.Patient(name: "Removed Patient", phoneNumber: "+15552222222");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, activePatient, removedPatient],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(doctor.Id, activePatient.Id),
                DoctorPatientBuilder.Create(doctor.Id, removedPatient.Id, isEnabled: false),
            ]);

        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientsAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().Id.Should().Be(activePatient.Id);
    }
}

public class PatientServiceGetPatientByIdTests
{
    [Fact]
    public async Task GetPatientByIdAsync_WhenPatientIsLinkedToDoctor_ReturnsPatientDto()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var assignedAt = DateTime.UtcNow.AddDays(-5);
        var relationship = DoctorPatientBuilder.Create(doctor.Id, patient.Id, assignedAt);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [relationship]);

        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientByIdAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(patient.Id);
        result.Value.Name.Should().Be(patient.Name);
        result.Value.PhoneNumber.Should().Be(patient.PhoneNumber);
        result.Value.FirstAssignedAt.Should().BeCloseTo(assignedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetPatientByIdAsync_WhenPatientNotLinkedToDoctor_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new PatientService(dbContext.Object);

        // Act
        var result = await sut.GetPatientByIdAsync(doctor.Id, patient.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found or is not linked to this doctor.");
    }
}
