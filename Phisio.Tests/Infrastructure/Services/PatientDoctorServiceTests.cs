using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientDoctorServiceTests
{
    [Fact]
    public async Task SearchDoctorsAsync_ReturnsEnabledDoctorsWithRelationshipStatus()
    {
        var patient = ApplicationUserBuilder.Patient();
        var linkedDoctor = ApplicationUserBuilder.Doctor(name: "Dr Linked", phoneNumber: "+15551111111");
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Dr Other", phoneNumber: "+15552222222");
        var disabledDoctor = ApplicationUserBuilder.Doctor(name: "Dr Disabled", phoneNumber: "+15553333333");
        disabledDoctor.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, linkedDoctor, otherDoctor, disabledDoctor],
            doctorProfiles:
            [
                DoctorProfileBuilder.Create(linkedDoctor.Id, specialty: "Physio"),
                DoctorProfileBuilder.Create(
                    otherDoctor.Id,
                    specialty: "Ortho",
                    medicalLicenseNumber: "MD-2"),
            ],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    linkedDoctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.SearchDoctorsAsync(patient.Id, search: null, specialty: null);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Single(item => item.DoctorId == linkedDoctor.Id)
            .RelationshipStatus.Should().Be(DoctorPatientStatus.Pending);
        result.Value.Single(item => item.DoctorId == otherDoctor.Id)
            .RelationshipStatus.Should().BeNull();
    }

    [Fact]
    public async Task RequestLinkAsync_CreatesPendingRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(patient.Id, doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(DoctorPatientStatus.Pending);
        dbContext.Object.DoctorPatients.Should().ContainSingle()
            .Which.Status.Should().Be(DoctorPatientStatus.Pending);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenAlreadyApproved_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(patient.Id, doctor.Id);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.AlreadyApproved);
    }

    [Fact]
    public async Task CancelRequestAsync_SoftDeletesPendingRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.CancelRequestAsync(patient.Id, doctor.Id);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.Should().BeEmpty();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UnlinkAsync_SoftDeletesApprovedRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.UnlinkAsync(patient.Id, doctor.Id);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyDoctorsAsync_ReturnsPendingAndApprovedOnly()
    {
        var patient = ApplicationUserBuilder.Patient();
        var approvedDoctor = ApplicationUserBuilder.Doctor(name: "Approved", phoneNumber: "+15551111111");
        var pendingDoctor = ApplicationUserBuilder.Doctor(name: "Pending", phoneNumber: "+15552222222");
        var rejectedDoctor = ApplicationUserBuilder.Doctor(name: "Rejected", phoneNumber: "+15553333333");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, approvedDoctor, pendingDoctor, rejectedDoctor],
            doctorProfiles:
            [
                DoctorProfileBuilder.Create(approvedDoctor.Id, medicalLicenseNumber: "MD-A"),
                DoctorProfileBuilder.Create(pendingDoctor.Id, medicalLicenseNumber: "MD-P"),
                DoctorProfileBuilder.Create(rejectedDoctor.Id, medicalLicenseNumber: "MD-R"),
            ],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(approvedDoctor.Id, patient.Id),
                DoctorPatientBuilder.Create(
                    pendingDoctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Pending),
                DoctorPatientBuilder.Create(
                    rejectedDoctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Rejected),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.GetMyDoctorsAsync(patient.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(item => item.DoctorId).Should().BeEquivalentTo(
            [approvedDoctor.Id, pendingDoctor.Id]);
    }
}
