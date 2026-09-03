using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class PatientDoctorServiceTests
{
    private static Mock<AppDbContext> CreateContextWithDoctorClinic(
        ApplicationUser patient,
        ApplicationUser doctor,
        IEnumerable<DoctorPatient>? doctorPatients = null,
        IEnumerable<DoctorProfile>? doctorProfiles = null)
    {
        var clinic = ClinicBuilder.CreateDefault(doctor.Id);
        var membership = ClinicBuilder.CreateMembership(clinic.ClinicId, doctor.Id);

        return AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: doctorProfiles,
            clinics: [clinic],
            clinicDoctors: [membership],
            doctorPatients: doctorPatients);
    }

    [Fact]
    public async Task SearchDoctorsAsync_ReturnsEnabledDoctorsWithRelationshipStatus()
    {
        var patient = ApplicationUserBuilder.Patient();
        var linkedDoctor = ApplicationUserBuilder.Doctor(name: "Dr Linked", phoneNumber: "+15551111111");
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Dr Other", phoneNumber: "+15552222222");
        var disabledDoctor = ApplicationUserBuilder.Doctor(name: "Dr Disabled", phoneNumber: "+15553333333");
        disabledDoctor.IsEnabled = false;

        var clinic = ClinicBuilder.CreateDefault(linkedDoctor.Id);
        var membership = ClinicBuilder.CreateMembership(clinic.ClinicId, linkedDoctor.Id);

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
            clinics: [clinic],
            clinicDoctors: [membership],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    linkedDoctor.Id,
                    patient.Id,
                    clinic.ClinicId,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.SearchDoctorsAsync(patient.Id, search: null, specialty: null);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Single(item => item.DoctorId == linkedDoctor.Id)
            .RelationshipStatus.Should().Be(DoctorPatientStatus.Pending);
        result.Value.Single(item => item.DoctorId == linkedDoctor.Id)
            .Clinics.Should().ContainSingle()
            .Which.ClinicId.Should().Be(clinic.ClinicId);
        result.Value.Single(item => item.DoctorId == otherDoctor.Id)
            .RelationshipStatus.Should().BeNull();
        result.Value.Single(item => item.DoctorId == otherDoctor.Id)
            .Clinics.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestLinkAsync_CreatesPendingRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = CreateContextWithDoctorClinic(
            patient,
            doctor,
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(
            patient.Id,
            doctor.Id,
            ClinicBuilder.DefaultClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(DoctorPatientStatus.Pending);
        result.Value.ClinicId.Should().Be(ClinicBuilder.DefaultClinicId);
        dbContext.Object.DoctorPatients.Should().ContainSingle()
            .Which.Status.Should().Be(DoctorPatientStatus.Pending);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenClinicNotFound_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(patient.Id, doctor.Id, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.ClinicNotFound);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenDoctorIsNotInClinic_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var clinic = ClinicBuilder.CreateDefault(doctor.Id);
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)],
            clinics: [clinic]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(patient.Id, doctor.Id, clinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.DoctorNotInClinic);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenAlreadyPending_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = CreateContextWithDoctorClinic(
            patient,
            doctor,
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(
            patient.Id,
            doctor.Id,
            ClinicBuilder.DefaultClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.AlreadyRequested);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenAlreadyApproved_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = CreateContextWithDoctorClinic(
            patient,
            doctor,
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(
            patient.Id,
            doctor.Id,
            ClinicBuilder.DefaultClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.AlreadyApproved);
    }

    [Fact]
    public async Task RequestLinkAsync_WhenPatientAlreadyLinkedElsewhere_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var firstClinic = ClinicBuilder.CreateDefault(doctor.Id);
        var secondClinic = ClinicBuilder.Create(managerId: doctor.Id, name: "Second Clinic");
        var memberships = new[]
        {
            ClinicBuilder.CreateMembership(firstClinic.ClinicId, doctor.Id),
            ClinicBuilder.CreateMembership(secondClinic.ClinicId, doctor.Id),
        };

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id)],
            clinics: [firstClinic, secondClinic],
            clinicDoctors: memberships,
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id, firstClinic.ClinicId)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.RequestLinkAsync(patient.Id, doctor.Id, secondClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.PatientAlreadyLinkedElsewhere);
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Should().HaveCount(1);
    }

    [Fact]
    public async Task CancelRequestAsync_SoftDeletesPendingRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = CreateContextWithDoctorClinic(
            patient,
            doctor,
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.CancelRequestAsync(
            patient.Id,
            doctor.Id,
            ClinicBuilder.DefaultClinicId);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.Should().BeEmpty();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UnlinkAsync_SoftDeletesApprovedRelationship()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var dbContext = CreateContextWithDoctorClinic(
            patient,
            doctor,
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.UnlinkAsync(
            patient.Id,
            doctor.Id,
            ClinicBuilder.DefaultClinicId);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.DoctorPatients.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CancelRequestAsync_WhenWrongClinic_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var firstClinic = ClinicBuilder.CreateDefault(doctor.Id);
        var secondClinic = ClinicBuilder.Create(managerId: doctor.Id, name: "Other Clinic");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            clinics: [firstClinic, secondClinic],
            clinicDoctors:
            [
                ClinicBuilder.CreateMembership(firstClinic.ClinicId, doctor.Id),
                ClinicBuilder.CreateMembership(secondClinic.ClinicId, doctor.Id),
            ],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    firstClinic.ClinicId,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.CancelRequestAsync(patient.Id, doctor.Id, secondClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.RequestNotFound);
        dbContext.Object.DoctorPatients.Should().ContainSingle();
    }

    [Fact]
    public async Task UnlinkAsync_WhenWrongClinic_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var firstClinic = ClinicBuilder.CreateDefault(doctor.Id);
        var secondClinic = ClinicBuilder.Create(managerId: doctor.Id, name: "Other Clinic");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            clinics: [firstClinic, secondClinic],
            clinicDoctors:
            [
                ClinicBuilder.CreateMembership(firstClinic.ClinicId, doctor.Id),
                ClinicBuilder.CreateMembership(secondClinic.ClinicId, doctor.Id),
            ],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id, firstClinic.ClinicId)]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.UnlinkAsync(patient.Id, doctor.Id, secondClinic.ClinicId);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.NotApproved);
        dbContext.Object.DoctorPatients.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDoctorClinicsAsync_ReturnsMembershipsWithPerClinicStatus()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var pendingClinic = ClinicBuilder.CreateDefault(doctor.Id);
        var openClinic = ClinicBuilder.Create(managerId: doctor.Id, name: "Open Clinic");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            clinics: [pendingClinic, openClinic],
            clinicDoctors:
            [
                ClinicBuilder.CreateMembership(pendingClinic.ClinicId, doctor.Id),
                ClinicBuilder.CreateMembership(openClinic.ClinicId, doctor.Id),
            ],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    pendingClinic.ClinicId,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.GetDoctorClinicsAsync(patient.Id, doctor.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Single(item => item.ClinicId == pendingClinic.ClinicId)
            .RelationshipStatus.Should().Be(DoctorPatientStatus.Pending);
        result.Value.Single(item => item.ClinicId == openClinic.ClinicId)
            .RelationshipStatus.Should().BeNull();
    }

    [Fact]
    public async Task GetDoctorClinicsAsync_WhenDoctorMissing_ReturnsFailure()
    {
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [patient]);
        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.GetDoctorClinicsAsync(patient.Id, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(DoctorPatientErrors.DoctorNotFound);
    }

    [Fact]
    public async Task GetDoctorProfileAsync_WithClinicId_ReturnsClinicScopedStatus()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var clinic = ClinicBuilder.CreateDefault(doctor.Id);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id, specialty: "Physio")],
            clinics: [clinic],
            clinicDoctors: [ClinicBuilder.CreateMembership(clinic.ClinicId, doctor.Id)],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    clinic.ClinicId,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.GetDoctorProfileAsync(patient.Id, doctor.Id, clinic.ClinicId);

        result.Succeeded.Should().BeTrue();
        result.Value!.ClinicId.Should().Be(clinic.ClinicId);
        result.Value.ClinicName.Should().Be(clinic.Name);
        result.Value.RelationshipStatus.Should().Be(DoctorPatientStatus.Pending);
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
        result.Value.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.ClinicName));
    }

    [Fact]
    public async Task GetMyDoctorsAsync_ReturnsSeparateRowsForSameDoctorInDifferentClinics()
    {
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor(name: "Multi Clinic", phoneNumber: "+15551111111");
        var firstClinic = ClinicBuilder.CreateDefault(doctor.Id);
        var secondClinic = ClinicBuilder.Create(managerId: doctor.Id, name: "Second Clinic");

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctor],
            doctorProfiles: [DoctorProfileBuilder.Create(doctor.Id, medicalLicenseNumber: "MD-1")],
            clinics: [firstClinic, secondClinic],
            clinicDoctors:
            [
                ClinicBuilder.CreateMembership(firstClinic.ClinicId, doctor.Id),
                ClinicBuilder.CreateMembership(secondClinic.ClinicId, doctor.Id),
            ],
            doctorPatients:
            [
                DoctorPatientBuilder.Create(doctor.Id, patient.Id, firstClinic.ClinicId),
                DoctorPatientBuilder.Create(
                    doctor.Id,
                    patient.Id,
                    secondClinic.ClinicId,
                    status: DoctorPatientStatus.Pending),
            ]);

        var sut = new PatientDoctorService(dbContext.Object);

        var result = await sut.GetMyDoctorsAsync(patient.Id);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(item => item.ClinicId).Should().BeEquivalentTo(
            [firstClinic.ClinicId, secondClinic.ClinicId]);
    }
}
