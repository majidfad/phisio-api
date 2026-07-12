using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;

using Phisio.Domain.Enums;

using Phisio.Infrastructure.Identity;



namespace Phisio.Infrastructure.Persistence.Seeding;



public class IdentitySeeder

{

    public const string DefaultAdminEmail = "admin@phisio.com";

    public const string DefaultAdminPhoneNumber = "+10000000000";

    public const string DefaultAdminPassword = "Admin123!";

    public const string DefaultAdminName = "System Administrator";



    private static readonly string AdminRoleName = nameof(UserRole.Admin);



    private readonly UserManager<ApplicationUser> _userManager;

    private readonly RoleManager<ApplicationRole> _roleManager;



    public IdentitySeeder(

        UserManager<ApplicationUser> userManager,

        RoleManager<ApplicationRole> roleManager)

    {

        _userManager = userManager;

        _roleManager = roleManager;

    }



    public async Task SeedAsync(CancellationToken cancellationToken = default)

    {

        cancellationToken.ThrowIfCancellationRequested();



        await EnsureRoleExistsAsync(cancellationToken);

        await NormalizeStoredPhoneNumbersAsync(cancellationToken);



        var existingUser = await FindDefaultAdminAsync(cancellationToken);

        if (existingUser is not null)

        {

            await EnsureDefaultAdminCredentialsAsync(existingUser, cancellationToken);

            await EnsureAdminRoleAssignedAsync(existingUser, cancellationToken);

            return;

        }



        var adminUser = new ApplicationUser

        {

            Id = Guid.NewGuid(),

            Name = DefaultAdminName,

            Role = UserRole.Admin,

            CreatedAt = DateTime.UtcNow

        };



        UserCredentials.Apply(adminUser, DefaultAdminPhoneNumber, DefaultAdminEmail);



        var createResult = await _userManager.CreateAsync(adminUser, DefaultAdminPassword);

        if (!createResult.Succeeded)

        {

            throw new InvalidOperationException(

                $"Failed to create default admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        }



        var addRoleResult = await _userManager.AddToRoleAsync(adminUser, AdminRoleName);

        if (!addRoleResult.Succeeded)

        {

            await _userManager.DeleteAsync(adminUser);

            throw new InvalidOperationException(

                $"Failed to assign Admin role to default admin user: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");

        }

    }



    private async Task NormalizeStoredPhoneNumbersAsync(CancellationToken cancellationToken)

    {

        cancellationToken.ThrowIfCancellationRequested();



        var users = await _userManager.Users.ToListAsync(cancellationToken);



        foreach (var user in users)

        {

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))

            {

                continue;

            }



            var canonicalPhone = UserCredentials.NormalizePhone(user.PhoneNumber);

            if (user.PhoneNumber == canonicalPhone && user.UserName == canonicalPhone)

            {

                continue;

            }



            UserCredentials.Apply(user, canonicalPhone, user.Email);



            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)

            {

                throw new InvalidOperationException(

                    $"Failed to normalize phone number for user '{user.Id}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");

            }

        }

    }



    private async Task<ApplicationUser?> FindDefaultAdminAsync(CancellationToken cancellationToken)

    {

        var userByPhone = await _userManager.Users

            .FirstOrDefaultAsync(

                u => u.PhoneNumber == DefaultAdminPhoneNumber || u.UserName == DefaultAdminPhoneNumber,

                cancellationToken);



        if (userByPhone is not null)

        {

            return userByPhone;

        }



        return await _userManager.FindByEmailAsync(DefaultAdminEmail);

    }



    private async Task EnsureDefaultAdminCredentialsAsync(

        ApplicationUser user,

        CancellationToken cancellationToken)

    {

        cancellationToken.ThrowIfCancellationRequested();



        var needsUpdate = user.PhoneNumber != DefaultAdminPhoneNumber

            || user.UserName != DefaultAdminPhoneNumber

            || user.Email != DefaultAdminEmail;



        if (!needsUpdate)

        {

            return;

        }



        UserCredentials.Apply(user, DefaultAdminPhoneNumber, DefaultAdminEmail);



        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)

        {

            throw new InvalidOperationException(

                $"Failed to update default admin user credentials: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");

        }

    }



    private async Task EnsureRoleExistsAsync(CancellationToken cancellationToken)

    {

        cancellationToken.ThrowIfCancellationRequested();



        if (await _roleManager.RoleExistsAsync(AdminRoleName))

        {

            return;

        }



        var createRoleResult = await _roleManager.CreateAsync(

            new ApplicationRole { Id = Guid.NewGuid(), Name = AdminRoleName });



        if (!createRoleResult.Succeeded)

        {

            throw new InvalidOperationException(

                $"Failed to create role '{AdminRoleName}': {string.Join(", ", createRoleResult.Errors.Select(e => e.Description))}");

        }

    }



    private async Task EnsureAdminRoleAssignedAsync(

        ApplicationUser user,

        CancellationToken cancellationToken)

    {

        cancellationToken.ThrowIfCancellationRequested();



        if (user.Role != UserRole.Admin)

        {

            user.Role = UserRole.Admin;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)

            {

                throw new InvalidOperationException(

                    $"Failed to update default admin user role: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");

            }

        }



        if (!await _userManager.IsInRoleAsync(user, AdminRoleName))

        {

            var addRoleResult = await _userManager.AddToRoleAsync(user, AdminRoleName);

            if (!addRoleResult.Succeeded)

            {

                throw new InvalidOperationException(

                    $"Failed to assign Admin role to existing admin user: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");

            }

        }

    }

}

