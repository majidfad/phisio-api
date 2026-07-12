using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Infrastructure.Identity;

namespace Phisio.Tests.MockFactory;

internal static class UserManagerMockExtensions
{
    public static void SetupRoleMapping(this Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager.Setup(manager => manager.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => [user.Role.ToString()]);
    }

    public static void SetupSuccessfulUserCreation(this Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    public static void SetupSuccessfulUserUpdate(this Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager.Setup(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    public static void SetupSuccessfulUserDeletion(this Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager.Setup(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
    }
}
