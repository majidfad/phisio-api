using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the Admin role and, when no active admin exists, the initial admin user.
/// Credentials come exclusively from <see cref="SeedAdminOptions"/> (the "SeedAdmin"
/// configuration section) — nothing is hardcoded. Safe to run on every startup.
/// </summary>
public class IdentitySeeder
{
    public const string AdminName = "System Administrator";

    private static readonly string AdminRoleName = nameof(UserRole.Admin);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SeedAdminOptions _options;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<SeedAdminOptions> options,
        ILogger<IdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureRoleExistsAsync(cancellationToken);

        var adminExists = await _userManager.Users
            .AnyAsync(u => u.Role == UserRole.Admin && u.IsEnabled, cancellationToken);

        if (adminExists)
        {
            _logger.LogInformation("Admin already exists. Skipping admin seed.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.PhoneNumber)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning(
                "Admin seed skipped because configuration is missing. " +
                "Set SeedAdmin__PhoneNumber and SeedAdmin__Password to create the initial admin.");
            return;
        }

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = AdminName,
            Role = UserRole.Admin,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        UserCredentials.Apply(adminUser, _options.PhoneNumber, email: null);

        var createResult = await _userManager.CreateAsync(adminUser, _options.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning(
                "Admin seed skipped because the configured credentials are invalid: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var addRoleResult = await _userManager.AddToRoleAsync(adminUser, AdminRoleName);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(adminUser);
            _logger.LogError(
                "Admin seed failed while assigning the Admin role: {Errors}",
                string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation("Initial admin user created successfully.");
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
}
