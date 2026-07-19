using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Application.Admin.Doctors;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;
using Phisio.Tests.TestHelpers;

namespace Phisio.Tests.Infrastructure.Services;

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(request.Name);
        result.Value.PhoneNumber.Should().Be(request.PhoneNumber);
        result.Value.Specialty.Should().Be(request.Specialty);
        result.Value.MedicalLicenseNumber.Should().Be(request.MedicalLicenseNumber);
        result.Value.ClinicAddress.Should().Be(request.ClinicAddress);

        var savedProfile = await dbContext.DoctorProfiles.SingleAsync();
        savedProfile.Specialty.Should().Be(request.Specialty);
        savedProfile.DoctorId.Should().Be(result.Value.Id);
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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Phone number is already registered.");
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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

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

        var sut = new AdminDoctorService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeactivateAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Doctor not found.");
    }
}
