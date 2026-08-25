using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Application.Doctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end harness for clinic management workflows:
/// real EF InMemory + ClinicService, exercised through ClinicsController.
/// </summary>
internal sealed class ClinicManagementTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ClinicManagementTestHost(ServiceProvider provider, AppDbContext dbContext)
    {
        _provider = provider;
        DbContext = dbContext;
    }

    public AppDbContext DbContext { get; }

    public static async Task<ClinicManagementTestHost> CreateAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var databaseName = $"clinic-mgmt-{Guid.NewGuid()}";

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IClinicService, ClinicService>();

        var doctorService = new Mock<IAdminDoctorService>();
        doctorService
            .Setup(service => service.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorDto>>.Success([]));
        services.AddSingleton(doctorService.Object);

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedRolesAsync(provider);

        return new ClinicManagementTestHost(provider, dbContext);
    }

    public ClinicsController CreateClinicsController(Guid? userId, params string[] roles)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = claims.Count > 0
            ? new ClaimsIdentity(claims, authenticationType: "Test")
            : new ClaimsIdentity();

        return new ClinicsController(
            _provider.GetRequiredService<IClinicService>(),
            _provider.GetRequiredService<IAdminDoctorService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            },
        };
    }

    public ClinicsController CreateAdminController(Guid adminId) =>
        CreateClinicsController(adminId, RoleNames.Admin);

    public ClinicsController CreateManagerController(Guid managerId) =>
        CreateClinicsController(managerId, RoleNames.ClinicManager, RoleNames.Doctor);

    public ClinicsController CreateDoctorController(Guid doctorId) =>
        CreateClinicsController(doctorId, RoleNames.Doctor);

    public T GetRequiredService<T>()
        where T : notnull =>
        _provider.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    private static async Task SeedRolesAsync(IServiceProvider provider)
    {
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in new[]
                 {
                     RoleNames.Admin,
                     RoleNames.Doctor,
                     RoleNames.Patient,
                     RoleNames.ClinicManager,
                 })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(
                    new ApplicationRole
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName,
                        NormalizedName = roleName.ToUpperInvariant(),
                    });

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to seed role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}

internal sealed record ClinicSeedResult(
    ApplicationUser Admin,
    ApplicationUser ManagerDoctor,
    ApplicationUser? MemberDoctor,
    Clinic? Clinic)
{
    public Guid ClinicId => Clinic?.ClinicId ?? Guid.Empty;
}

internal static class ClinicManagementTestHostSeeder
{
    public static async Task<ApplicationUser> SeedAdminAsync(ClinicManagementTestHost host)
    {
        var admin = ApplicationUserBuilder.Admin(phoneNumber: "+15550000001");
        host.DbContext.Users.Add(admin);

        var adminRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
        });

        await host.DbContext.SaveChangesAsync();
        return admin;
    }

    public static async Task<ApplicationUser> SeedDoctorAsync(
        ClinicManagementTestHost host,
        string name = "Dr. Manager",
        string phoneNumber = "+15552000001",
        bool grantClinicManagerIdentityRole = false)
    {
        var doctor = ApplicationUserBuilder.Doctor(name: name, phoneNumber: phoneNumber);
        host.DbContext.Users.Add(doctor);
        host.DbContext.DoctorProfiles.Add(DoctorProfileBuilder.Create(doctor.Id));

        var doctorRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Doctor);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = doctor.Id,
            RoleId = doctorRole.Id,
        });

        if (grantClinicManagerIdentityRole)
        {
            var clinicManagerRole = await host.DbContext.Roles
                .SingleAsync(role => role.Name == RoleNames.ClinicManager);
            host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = doctor.Id,
                RoleId = clinicManagerRole.Id,
            });
        }

        await host.DbContext.SaveChangesAsync();
        return doctor;
    }

    public static async Task<ApplicationUser> SeedPatientAsync(
        ClinicManagementTestHost host,
        string phoneNumber = "+15551000001")
    {
        var patient = ApplicationUserBuilder.Patient(phoneNumber: phoneNumber);
        host.DbContext.Users.Add(patient);

        var patientRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Patient);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = patient.Id,
            RoleId = patientRole.Id,
        });

        await host.DbContext.SaveChangesAsync();
        return patient;
    }

    public static async Task<ClinicSeedResult> SeedClinicWithManagerAsync(
        ClinicManagementTestHost host,
        string clinicName = "North Clinic",
        string address = "North Address",
        string phoneNumber = "02111110001",
        bool includeMemberDoctor = false)
    {
        var admin = await SeedAdminAsync(host);
        var manager = await SeedDoctorAsync(
            host,
            name: "Dr. Manager",
            phoneNumber: "+15552000001",
            grantClinicManagerIdentityRole: true);

        ApplicationUser? member = null;
        if (includeMemberDoctor)
        {
            member = await SeedDoctorAsync(
                host,
                name: "Dr. Member",
                phoneNumber: "+15552000002");
        }

        var clinic = ClinicBuilder.Create(managerId: manager.Id, name: clinicName, address: address);
        host.DbContext.Clinics.Add(clinic);
        host.DbContext.ClinicPhoneNumbers.Add(new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            ClinicId = clinic.ClinicId,
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber),
        });
        host.DbContext.ClinicDoctors.Add(ClinicBuilder.CreateMembership(clinic.ClinicId, manager.Id));

        if (member is not null)
        {
            host.DbContext.ClinicDoctors.Add(ClinicBuilder.CreateMembership(clinic.ClinicId, member.Id));
        }

        await host.DbContext.SaveChangesAsync();

        return new ClinicSeedResult(admin, manager, member, clinic);
    }

    public static async Task<bool> HasIdentityRoleAsync(
        ClinicManagementTestHost host,
        Guid userId,
        string roleName)
    {
        var roleId = await host.DbContext.Roles
            .Where(role => role.Name == roleName)
            .Select(role => role.Id)
            .SingleAsync();

        return await host.DbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == userId && userRole.RoleId == roleId);
    }
}

/// <summary>
/// Simulates persistence failure for Clinic / ClinicDoctor / ClinicPhoneNumber / UserRole changes.
/// </summary>
internal sealed class FailingClinicSaveInterceptor : SaveChangesInterceptor
{
    public bool FailOnNextClinicRelatedSave { get; set; }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfConfigured(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ThrowIfConfigured(DbContext? context)
    {
        if (!FailOnNextClinicRelatedSave || context is null)
        {
            return;
        }

        var hasRelevantChange =
            context.ChangeTracker.Entries<Clinic>().Any(IsPending) ||
            context.ChangeTracker.Entries<ClinicDoctor>().Any(IsPending) ||
            context.ChangeTracker.Entries<ClinicPhoneNumber>().Any(IsPending) ||
            context.ChangeTracker.Entries<IdentityUserRole<Guid>>().Any(IsPending);

        if (!hasRelevantChange)
        {
            return;
        }

        FailOnNextClinicRelatedSave = false;
        throw new InvalidOperationException("Simulated clinic persistence failure.");
    }

    private static bool IsPending(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
}

internal static class ClinicManagementTestHostExtensions
{
    public static void UseFailingClinicSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingClinicSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"clinic-mgmt-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }
}
