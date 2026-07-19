using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence.Seeding;
using Phisio.Tests.MockFactory;

namespace Phisio.Tests.Infrastructure.Persistence.Seeding;

public class IdentitySeederTests
{
    private const string ConfiguredPhoneNumber = "+10000000000";
    private const string ConfiguredPassword = "Admin123!";

    [Fact]
    public async Task SeedAsync_WhenNoAdminExists_CreatesAdmin()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = CreateRoleManagerWithExistingRole();

        ApplicationUser? createdUser = null;
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword))
            .Callback<ApplicationUser, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Admin)))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, ConfiguredPassword);
        var beforeSeed = DateTime.UtcNow;

        // Act
        await sut.SeedAsync();

        // Assert
        createdUser.Should().NotBeNull();
        createdUser!.UserName.Should().Be(ConfiguredPhoneNumber);
        createdUser.PhoneNumber.Should().Be(ConfiguredPhoneNumber);
        createdUser.Name.Should().Be(IdentitySeeder.AdminName);
        createdUser.Role.Should().Be(UserRole.Admin);
        createdUser.IsEnabled.Should().BeTrue();
        createdUser.CreatedAt.Should().BeOnOrAfter(beforeSeed);

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword),
            Times.Once);
        userManager.Verify(
            manager => manager.AddToRoleAsync(createdUser, nameof(UserRole.Admin)),
            Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenActiveAdminExists_DoesNotCreateDuplicate()
    {
        // Arrange
        var existingAdmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Existing Admin",
            Role = UserRole.Admin,
            IsEnabled = true,
            PhoneNumber = "+19999999999",
            UserName = "+19999999999"
        };

        var userManager = IdentityMockFactory.CreateUserManager([existingAdmin]);
        var roleManager = CreateRoleManagerWithExistingRole();

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, ConfiguredPassword);

        // Act
        await sut.SeedAsync();

        // Assert
        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenOnlyDisabledAdminExists_CreatesNewAdmin()
    {
        // Arrange
        var disabledAdmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Disabled Admin",
            Role = UserRole.Admin,
            IsEnabled = false,
            PhoneNumber = "+19999999999",
            UserName = "+19999999999"
        };

        var userManager = IdentityMockFactory.CreateUserManager([disabledAdmin]);
        var roleManager = CreateRoleManagerWithExistingRole();

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Admin)))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, ConfiguredPassword);

        // Act
        await sut.SeedAsync();

        // Assert
        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword),
            Times.Once);
    }

    [Theory]
    [InlineData(null, ConfiguredPassword)]
    [InlineData("", ConfiguredPassword)]
    [InlineData("   ", ConfiguredPassword)]
    [InlineData(ConfiguredPhoneNumber, null)]
    [InlineData(ConfiguredPhoneNumber, "")]
    [InlineData(ConfiguredPhoneNumber, "   ")]
    public async Task SeedAsync_WhenConfigurationMissing_SkipsSeedingAndLogsWarning(
        string? phoneNumber,
        string? password)
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = CreateRoleManagerWithExistingRole();
        var logger = new Mock<ILogger<IdentitySeeder>>();

        var sut = CreateSeeder(userManager, roleManager, phoneNumber, password, logger);

        // Act
        var act = () => sut.SeedAsync();

        // Assert
        await act.Should().NotThrowAsync();

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);

        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenConfiguredPasswordIsRejected_SkipsWithoutThrowing()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = CreateRoleManagerWithExistingRole();

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Passwords must be at least 8 characters."
            }));

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, "weak");

        // Act
        var act = () => sut.SeedAsync();

        // Assert
        await act.Should().NotThrowAsync();

        userManager.Verify(
            manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenRoleAssignmentFails_DeletesCreatedUser()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = CreateRoleManagerWithExistingRole();

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Admin)))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));
        userManager.Setup(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, ConfiguredPassword);

        // Act
        var act = () => sut.SeedAsync();

        // Assert
        await act.Should().NotThrowAsync();

        userManager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenAdminRoleMissing_CreatesRole()
    {
        // Arrange
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Admin)))
            .ReturnsAsync(false);
        roleManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), ConfiguredPassword))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Admin)))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSeeder(userManager, roleManager, ConfiguredPhoneNumber, ConfiguredPassword);

        // Act
        await sut.SeedAsync();

        // Assert
        roleManager.Verify(
            manager => manager.CreateAsync(It.Is<ApplicationRole>(role => role.Name == nameof(UserRole.Admin))),
            Times.Once);
    }

    private static Mock<RoleManager<ApplicationRole>> CreateRoleManagerWithExistingRole()
    {
        var roleManager = IdentityMockFactory.CreateRoleManager();
        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Admin)))
            .ReturnsAsync(true);
        return roleManager;
    }

    private static IdentitySeeder CreateSeeder(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<RoleManager<ApplicationRole>> roleManager,
        string? phoneNumber,
        string? password,
        Mock<ILogger<IdentitySeeder>>? logger = null)
    {
        var options = Options.Create(new SeedAdminOptions
        {
            PhoneNumber = phoneNumber,
            Password = password
        });

        return new IdentitySeeder(
            userManager.Object,
            roleManager.Object,
            options,
            (logger ?? new Mock<ILogger<IdentitySeeder>>()).Object);
    }
}
