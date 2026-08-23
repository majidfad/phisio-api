using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;
using Phisio.Tests.TestHelpers;

namespace Phisio.Tests.Infrastructure.Services;

internal static class AdminDoctorServiceTestHelper
{
    public static Mock<IClinicService> CreateClinicServiceMock()
    {
        var clinicService = new Mock<IClinicService>();
        clinicService
            .Setup(service => service.LookupByPhonesAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<LookupClinicsByPhonesDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicPhoneLookupResultDto>.Success(
                new ClinicPhoneLookupResultDto(ClinicPhoneLookupStatus.None, null, [])));

        clinicService
            .Setup(service => service.AssignDoctorAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<AssignDoctorToClinicDto>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ClinicAccessContext _,
                AssignDoctorToClinicDto request,
                CancellationToken _) =>
                Task.FromResult(AuthResult<AssignDoctorToClinicResultDto>.Success(
                    new AssignDoctorToClinicResultDto(
                        new ClinicDto(
                            Guid.NewGuid(),
                            request.Name ?? "Clinic",
                            request.Address ?? "Address",
                            request.DoctorId,
                            request.PhoneNumbers.ToList(),
                            DateTime.UtcNow),
                        new ClinicDoctorMemberDto(
                            request.DoctorId,
                            "Doctor",
                            "+15550000000",
                            UserRole.Doctor,
                            "Specialty",
                            IsClinicManager: true),
                        ClinicCreated: true))));

        return clinicService;
    }

    public static AdminDoctorService Create(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        Mock<IClinicService>? clinicService = null) =>
        new(
            dbContext,
            userManager,
            roleManager,
            (clinicService ?? CreateClinicServiceMock()).Object);
}

public class AdminDoctorServiceGetAllTests
{
    [Fact]
    public async Task GetAllAsync_WhenNoDoctorsExist_ReturnsEmptyList()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        var userManager = IdentityMockFactory.CreateUserManager([patient]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenDoctorsExist_ReturnsDoctorsOrderedByName()
    {
        // Arrange
        var charlie = ApplicationUserBuilder.Doctor(name: "Dr. Charlie");
        var alice = ApplicationUserBuilder.Doctor(name: "Dr. Alice");
        alice.CreatedAt = DateTime.UtcNow.AddDays(-3);
        var aliceProfile = DoctorProfileBuilder.Create(
            alice.Id,
            specialty: "Orthopedics",
            medicalLicenseNumber: "MD-11111",
            clinicAddress: "Clinic A",
            createdAt: DateTime.UtcNow.AddDays(-2));

        var userManager = IdentityMockFactory.CreateUserManager([charlie, alice]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [charlie, alice],
            doctorProfiles: [aliceProfile]).Object;

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(2);
        result.Value.Select(dto => dto.Name).Should().ContainInOrder("Dr. Alice", "Dr. Charlie");

        var aliceDto = result.Value.First(dto => dto.Id == alice.Id);
        aliceDto.Specialty.Should().Be("Orthopedics");
        aliceDto.MedicalLicenseNumber.Should().Be("MD-11111");
        aliceDto.ClinicAddress.Should().Be("Clinic A");
        aliceDto.CreatedAt.Should().BeCloseTo(aliceProfile.CreatedAt, TimeSpan.FromSeconds(1));

        var charlieDto = result.Value.First(dto => dto.Id == charlie.Id);
        charlieDto.Specialty.Should().BeEmpty();
        charlieDto.MedicalLicenseNumber.Should().BeEmpty();
        charlieDto.ClinicAddress.Should().BeEmpty();
        charlieDto.CreatedAt.Should().BeCloseTo(charlie.CreatedAt, TimeSpan.FromSeconds(1));
        charlieDto.IsClinicManager.Should().BeFalse();
        charlieDto.ManagedClinicNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenClinicManagersExist_ReturnsManagedClinicNames()
    {
        var manager = ApplicationUserBuilder.ClinicManager(name: "Sara Manager");
        var doctor = ApplicationUserBuilder.Doctor(name: "Dr. Only");
        var northClinic = new Domain.Entities.Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "North Clinic",
            Address = "Tehran",
            ClinicManagerId = manager.Id,
        };
        northClinic.EnsureManagerDoctorMembership();
        var southClinic = new Domain.Entities.Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "South Clinic",
            Address = "Isfahan",
            ClinicManagerId = manager.Id,
        };
        southClinic.EnsureManagerDoctorMembership();

        var userManager = IdentityMockFactory.CreateUserManager([manager, doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [manager, doctor]).Object;
        dbContext.Clinics.AddRange(northClinic, southClinic);
        await dbContext.SaveChangesAsync();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        var result = await sut.GetAllAsync();

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var managerDto = result.Value!.Single(dto => dto.Id == manager.Id);
        managerDto.IsClinicManager.Should().BeTrue();
        managerDto.ManagedClinicNames.Should().Equal("North Clinic", "South Clinic");

        var doctorDto = result.Value.Single(dto => dto.Id == doctor.Id);
        doctorDto.IsClinicManager.Should().BeFalse();
        doctorDto.ManagedClinicNames.Should().BeNullOrEmpty();
    }
}

public class AdminDoctorServiceGetByIdTests
{
    [Fact]
    public async Task GetByIdAsync_WhenDoctorExists_ReturnsDoctorDto()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var profile = DoctorProfileBuilder.Create(doctor.Id);

        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(doctorProfiles: [profile]).Object;

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetByIdAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(doctor.Id);
        result.Value.Specialty.Should().Be(profile.Specialty);
    }

    [Fact]
    public async Task GetByIdAsync_WhenDoctorNotFound_ReturnsFailure()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Doctor not found.");
    }
}

public class AdminDoctorServiceCreateTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesDoctorAndProfile()
    {
        // Arrange
        var request = DoctorTestDataBuilder.CreateDto();

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Doctor)))
            .ReturnsAsync(true);
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Doctor)))
            .ReturnsAsync(IdentityResult.Success);

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Doctor.Name.Should().Be(request.Name);
        result.Value.Doctor.PhoneNumber.Should().Be(request.PhoneNumber);
        result.Value.Doctor.Specialty.Should().Be(request.Specialty);
        result.Value.Doctor.MedicalLicenseNumber.Should().Be(request.MedicalLicenseNumber);
        result.Value.Doctor.ClinicAddress.Should().Be(request.NewClinicAddress);
        result.Value.GeneratedPassword.Should().NotBeNullOrWhiteSpace();

        var savedProfile = await dbContext.DoctorProfiles.SingleAsync();
        savedProfile.Specialty.Should().Be(request.Specialty);
        savedProfile.DoctorId.Should().Be(result.Value.Doctor.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenPhoneAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var request = DoctorTestDataBuilder.CreateDto();
        var existingUser = ApplicationUserBuilder.Doctor(phoneNumber: request.PhoneNumber);

        var userManager = IdentityMockFactory.CreateUserManager([existingUser]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Doctor)))
            .ReturnsAsync(true);

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Phone number is already registered.");
    }

    [Fact]
    public async Task CreateAsync_WhenClinicPhonesConflict_ReturnsFailureWithoutCreatingDoctor()
    {
        // Arrange
        var request = DoctorTestDataBuilder.CreateDto();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();
        var clinicService = AdminDoctorServiceTestHelper.CreateClinicServiceMock();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Doctor)))
            .ReturnsAsync(true);
        clinicService
            .Setup(service => service.LookupByPhonesAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<LookupClinicsByPhonesDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicPhoneLookupResultDto>.Success(
                new ClinicPhoneLookupResultDto(ClinicPhoneLookupStatus.Conflict, null, [])));

        var sut = AdminDoctorServiceTestHelper.Create(
            dbContext,
            userManager.Object,
            roleManager.Object,
            clinicService);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(ClinicErrors.ConflictingClinicPhones);
        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenClinicExists_AssignsDoctorToExistingClinic()
    {
        // Arrange
        var request = DoctorTestDataBuilder.CreateDto();
        request.NewClinicName = null;
        request.NewClinicAddress = null;
        request.ManagerIsThisDoctor = false;

        var existingClinic = new ClinicDto(
            Guid.NewGuid(),
            "Vanak Clinic",
            "Vanak St",
            Guid.NewGuid(),
            ["02112345678"],
            DateTime.UtcNow);

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();
        var clinicService = AdminDoctorServiceTestHelper.CreateClinicServiceMock();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Doctor)))
            .ReturnsAsync(true);
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Doctor)))
            .ReturnsAsync(IdentityResult.Success);

        clinicService
            .Setup(service => service.LookupByPhonesAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<LookupClinicsByPhonesDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicPhoneLookupResultDto>.Success(
                new ClinicPhoneLookupResultDto(ClinicPhoneLookupStatus.Found, existingClinic, [])));

        var sut = AdminDoctorServiceTestHelper.Create(
            dbContext,
            userManager.Object,
            roleManager.Object,
            clinicService);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        clinicService.Verify(
            service => service.AssignDoctorAsync(
                It.IsAny<ClinicAccessContext>(),
                It.Is<AssignDoctorToClinicDto>(dto =>
                    dto.PhoneNumbers.SequenceEqual(request.ClinicPhoneNumbers)
                    && dto.Name == null
                    && dto.Address == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class AdminDoctorServiceUpdateTests
{
    [Fact]
    public async Task UpdateAsync_WhenDoctorExistsWithoutProfile_CreatesProfileAndReturnsDto()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var request = DoctorTestDataBuilder.UpdateDto();

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(doctor.Id.ToString()))
            .ReturnsAsync(doctor);
        userManager.SetupSuccessfulUserUpdate();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.UpdateAsync(doctor.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Specialty.Should().Be(request.Specialty);

        var savedProfile = await dbContext.DoctorProfiles.SingleAsync();
        savedProfile.DoctorId.Should().Be(doctor.Id);
        savedProfile.MedicalLicenseNumber.Should().Be(request.MedicalLicenseNumber);
    }

    [Fact]
    public async Task UpdateAsync_WhenDoctorNotFound_ReturnsFailure()
    {
        // Arrange
        var request = DoctorTestDataBuilder.UpdateDto();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Doctor not found.");
    }
}

public class AdminDoctorServiceDeleteTests
{
    [Fact]
    public async Task DeleteAsync_WhenDoctorExists_SoftDeletesDoctorProfileAndAssignments()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var profile = DoctorProfileBuilder.Create(doctor.Id);
        var assignment = AssignmentBuilder.Create(doctor.Id, Guid.NewGuid(), Guid.NewGuid());

        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor],
            userExercises: [assignment],
            doctorProfiles: [profile]).Object;

        userManager.SetupSuccessfulUserUpdate();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeleteAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        doctor.IsEnabled.Should().BeFalse();
        profile.IsEnabled.Should().BeFalse();
        assignment.IsEnabled.Should().BeFalse();
    }
}

public class AdminDoctorServiceActivateTests
{
    [Fact]
    public async Task ActivateAsync_WhenDoctorIsDisabled_RestoresDoctorAndProfile()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        doctor.IsEnabled = false;
        var profile = DoctorProfileBuilder.Create(doctor.Id);
        profile.IsEnabled = false;

        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor],
            doctorProfiles: [profile]).Object;

        userManager.SetupSuccessfulUserUpdate();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.ActivateAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        doctor.IsEnabled.Should().BeTrue();
        profile.IsEnabled.Should().BeTrue();
    }
}

public class AdminDoctorServiceDeactivateTests
{
    [Fact]
    public async Task DeactivateAsync_WhenDoctorIsActive_DisablesDoctorAndProfile()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var profile = DoctorProfileBuilder.Create(doctor.Id);

        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor],
            doctorProfiles: [profile]).Object;

        userManager.SetupSuccessfulUserUpdate();

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        doctor.IsEnabled.Should().BeFalse();
        profile.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_WhenDoctorIsAlreadyInactive_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        doctor.IsEnabled = false;

        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [doctor]).Object;

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeactivateAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Doctor is already inactive.");
        doctor.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_WhenDoctorDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock().Object;

        var sut = AdminDoctorServiceTestHelper.Create(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeactivateAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Doctor not found.");
    }
}
