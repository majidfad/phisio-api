using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Application.Admin.Patients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class AdminPatientServiceGetAllTests
{
    [Fact]
    public async Task GetAllAsync_WhenNoPatientsExist_ReturnsEmptyList()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenPatientsExist_ReturnsAllPatientsOrderedByName()
    {
        // Arrange
        var charlie = ApplicationUserBuilder.Patient(name: "Charlie Patient", phoneNumber: "+15553333333");
        charlie.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var alice = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        alice.CreatedAt = DateTime.UtcNow.AddDays(-3);
        var doctor = ApplicationUserBuilder.Doctor(name: "Dr. Smith");
        var exercise = ExerciseBuilder.Create();
        var assignment = AssignmentBuilder.Create(
            doctor.Id,
            alice.Id,
            exercise.ExerciseId,
            assignedAt: DateTime.UtcNow.AddDays(-2));

        var userManager = IdentityMockFactory.CreateUserManager([charlie, alice, doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [charlie, alice, doctor],
            userExercises: [assignment]).Object;

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(2);
        result.Value.Select(dto => dto.Name).Should().ContainInOrder("Alice Patient", "Charlie Patient");
        result.Value.First(dto => dto.Id == alice.Id).FirstAssignedAt.Should()
            .BeCloseTo(assignment.AssignedAt, TimeSpan.FromSeconds(1));
        result.Value.First(dto => dto.Id == charlie.Id).FirstAssignedAt.Should()
            .BeCloseTo(charlie.CreatedAt, TimeSpan.FromSeconds(1));
        result.Value.First(dto => dto.Id == alice.Id).DoctorNames.Should().ContainSingle()
            .Which.Should().Be("Dr. Smith");
        result.Value.First(dto => dto.Id == charlie.Id).DoctorNames.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenPatientHasMultipleDoctors_ReturnsDistinctOrderedDoctorNames()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551111111");
        var doctorBob = ApplicationUserBuilder.Doctor(name: "Bob Doctor", phoneNumber: "+15552222222");
        var doctorAnn = ApplicationUserBuilder.Doctor(name: "Ann Doctor", phoneNumber: "+15553333333");
        var exerciseOne = ExerciseBuilder.Create();
        var exerciseTwo = ExerciseBuilder.Create();
        var exerciseThree = ExerciseBuilder.Create();
        var assignments = new[]
        {
            AssignmentBuilder.Create(doctorAnn.Id, patient.Id, exerciseOne.ExerciseId),
            AssignmentBuilder.Create(doctorBob.Id, patient.Id, exerciseTwo.ExerciseId),
            AssignmentBuilder.Create(doctorBob.Id, patient.Id, exerciseThree.ExerciseId),
        };

        var userManager = IdentityMockFactory.CreateUserManager([patient, doctorBob, doctorAnn]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [patient, doctorBob, doctorAnn],
            userExercises: assignments).Object;

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Should().ContainSingle();
        result.Value.Single().DoctorNames.Should().Equal("Ann Doctor", "Bob Doctor");
    }
}

public class AdminPatientServiceGetByIdTests
{
    [Fact]
    public async Task GetByIdAsync_WhenPatientExists_ReturnsPatientDto()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        patient.CreatedAt = DateTime.UtcNow.AddDays(-4);

        var userManager = IdentityMockFactory.CreateUserManager([patient]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetByIdAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(patient.Id);
        result.Value.Name.Should().Be(patient.Name);
        result.Value.PhoneNumber.Should().Be(patient.PhoneNumber);
        result.Value.CreatedAt.Should().BeCloseTo(patient.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetByIdAsync(patientId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found.");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserIsDoctor_ReturnsFailure()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var userManager = IdentityMockFactory.CreateUserManager([doctor]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.GetByIdAsync(doctor.Id);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found.");
    }
}

public class AdminPatientServiceCreateTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesPatient()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111",
            Email = "alice@example.com"
        };

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);
        userManager.SetupSuccessfulUserCreation();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(request.Name);
        result.Value.PhoneNumber.Should().Be(request.PhoneNumber);
        result.Value.Email.Should().Be(request.Email);

        userManager.Verify(
            manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Name == request.Name
                    && user.Role == UserRole.Patient
                    && user.PhoneNumber == request.PhoneNumber),
                It.IsAny<string>()),
            Times.Once);

        userManager.Verify(
            manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Patient)),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPhoneAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111"
        };
        var existingUser = ApplicationUserBuilder.Patient(phoneNumber: request.PhoneNumber);

        var userManager = IdentityMockFactory.CreateUserManager([existingUser]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Phone number is already registered.");
    }
}

public class AdminPatientServiceUpdateTests
{
    [Fact]
    public async Task UpdateAsync_WhenPatientExists_ReturnsUpdatedPatientDto()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        patient.CreatedAt = DateTime.UtcNow.AddDays(-5);
        var request = new UpdateAdminPatientDto
        {
            Name = "Updated Patient",
            PhoneNumber = "+15559999999",
            Email = "updated@example.com"
        };

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(patient.Id.ToString()))
            .ReturnsAsync(patient);
        userManager.SetupSuccessfulUserUpdate();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.UpdateAsync(patient.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(request.Name);
        result.Value.PhoneNumber.Should().Be(request.PhoneNumber);
        result.Value.Email.Should().Be(request.Email);

        userManager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var request = new UpdateAdminPatientDto
        {
            Name = "Updated Patient",
            PhoneNumber = "+15559999999"
        };

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(patientId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.UpdateAsync(patientId, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found.");
    }
}

public class AdminPatientServiceDeleteTests
{
    [Fact]
    public async Task DeleteAsync_WhenPatientExists_SoftDeletesPatientAndAssignments()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        var doctor = ApplicationUserBuilder.Doctor();
        var exercise = ExerciseBuilder.Create();
        var assignment = AssignmentBuilder.Create(doctor.Id, patient.Id, exercise.ExerciseId);

        var userManager = IdentityMockFactory.CreateUserManager([patient]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create(userExercises: [assignment]);

        userManager.SetupSuccessfulUserUpdate();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeleteAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        patient.IsEnabled.Should().BeFalse();
        dbContext.UserExercises.Should().BeEmpty();

        var disabledAssignment = await dbContext.UserExercises.IgnoreQueryFilters().SingleAsync();
        disabledAssignment.IsEnabled.Should().BeFalse();

        userManager.Verify(manager => manager.UpdateAsync(patient), Times.Once);
        userManager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenPatientNotFound_ReturnsFailure()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.Create();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.DeleteAsync(patientId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Patient not found.");
    }
}

public class AdminPatientServiceActivateTests
{
    [Fact]
    public async Task ActivateAsync_WhenPatientIsDisabled_RestoresPatient()
    {
        // Arrange
        var patient = ApplicationUserBuilder.Patient();
        patient.IsEnabled = false;

        var userManager = IdentityMockFactory.CreateUserManager([patient]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var dbContext = AppDbContextMockFactory.CreateMock(users: [patient]).Object;

        userManager.SetupSuccessfulUserUpdate();

        var sut = new AdminPatientService(dbContext, userManager.Object, roleManager.Object);

        // Act
        var result = await sut.ActivateAsync(patient.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        patient.IsEnabled.Should().BeTrue();
    }
}
