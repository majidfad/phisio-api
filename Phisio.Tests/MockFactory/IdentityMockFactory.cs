using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.TestHelpers;

namespace Phisio.Tests.MockFactory;

internal static class IdentityMockFactory
{
    public static Mock<UserManager<ApplicationUser>> CreateUserManager(
        IEnumerable<ApplicationUser>? users = null)
    {
        users ??= [];

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        userManager.Setup(manager => manager.Users).Returns(users.AsAsyncQueryable());

        return userManager;
    }

    public static Mock<RoleManager<ApplicationRole>> CreateRoleManager()
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            store.Object,
            null!,
            null!,
            null!,
            null!);

        return roleManager;
    }
}
