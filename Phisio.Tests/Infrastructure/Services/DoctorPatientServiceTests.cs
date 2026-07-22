using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceGetPatientsTests
{
    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasNoPatients_ReturnsEmptyList()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]);
        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.GetPatientsAsync(doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientsAsync_WhenDoctorHasPatients_ReturnsApprovedRelationshipsOnly()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var activePatient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var inactivePatient = ApplicationUserBuilder.Patient(name: "Bob Patient", phoneNumber: "+15552222222");
        var pendingPatient = ApplicationUserBuilder.Patient(name: "Carol Patient", phoneNumber: "+15553333333");
        var assignedAt = DateTime.UtcNow.AddDays(-3);

        var doctorPatients = new[]
        {
            DoctorPatientBuilder.Create(doctor.Id, activePatient.Id, assignedAt),
            DoctorPatientBuilder.Create(doctor.Id, inactivePatient.Id, isEnabled: false),
            DoctorPatientBuilder.Create(
                doctor.Id,
                pendingPatient.Id,
                status: DoctorPatientStatus.Pending),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, activePatient, inactivePatient, pendingPatient],
            doctorPatients: doctorPatients);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.GetPatientsAsync(doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().PatientId.Should().Be(activePatient.Id);
        result.Value.Single().PatientName.Should().Be(activePatient.Name);
        result.Value.Single().PhoneNumber.Should().Be(activePatient.PhoneNumber);
        result.Value.Single().AssignedAt.Should().BeCloseTo(assignedAt, TimeSpan.FromSeconds(1));
    }
}

public class DoctorPatientServiceRequestLifecycleTests
{
    [Fact]
    public async Task GetPendingRequestsAsync_ReturnsPendingPatientsOnly()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var pendingPatient = ApplicationUserBuilder.Patient(name: "Pending", phoneNumber: "+15551111111");
        var approvedPatient = ApplicationUserBuilder.Patient(name: "Approved", phoneNumber: "+15552222222");
        var requestedAt = DateTime.UtcNow.AddHours(-2);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, pendingPatient, approvedPatient],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    pendingPatient.Id,
                    requestedAt,
                    status: DoctorPatientStatus.Pending),
                DoctorPatientBuilder.Create(doctor.Id, approvedPatient.Id),
            ]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.GetPendingRequestsAsync(doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().PatientId.Should().Be(pendingPatient.Id);
        result.Value.Single().RequestedAt.Should().BeCloseTo(requestedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenPending_ApprovesRelationship()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(
            doctor.Id,
            patient.Id,
            status: DoctorPatientStatus.Pending);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.ApproveRequestAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeTrue();
        result.Value!.PatientId.Should().Be(patient.Id);
        dbContext.Object.DoctorPatients.Single().Status.Should().Be(DoctorPatientStatus.Approved);
    }

    [Fact]
    public async Task RejectRequestAsync_WhenPending_SetsRejectedStatus()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(
            doctor.Id,
            patient.Id,
            status: DoctorPatientStatus.Pending);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.RejectRequestAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.Single().Status.Should().Be(DoctorPatientStatus.Rejected);
        dbContext.Object.DoctorPatients.Single().IsEnabled.Should().BeTrue();
    }
}

public class DoctorPatientServiceRemoveTests
{
    [Fact]
    public async Task RemoveAsync_WhenRelationshipExists_SoftDeletesRelationship()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(doctor.Id, patient.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.RemoveAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.Should().BeEmpty();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_WhenRelationshipMissing_ReturnsFailure()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor, patient]);
        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.RemoveAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientService.RelationshipNotFoundError);
    }

    [Fact]
    public async Task RemoveAsync_WhenAlreadySoftDeleted_ReturnsFailure()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient(phoneNumber: "+15551111111");
        var existing = DoctorPatientBuilder.Create(doctor.Id, patient.Id, isEnabled: false);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [existing]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.RemoveAsync(doctor.Id, patient.Id);

        result.Succeeded.Should().BeFalse();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }
}
