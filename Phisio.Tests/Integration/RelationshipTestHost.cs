using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Phisio.Api.Controllers.Doctor;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end harness for patient–doctor–clinic relationship workflows:
/// real EF InMemory + relationship services, exercised through API controllers.
/// </summary>
internal sealed class RelationshipTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private RelationshipTestHost(ServiceProvider provider, AppDbContext dbContext)
    {
        _provider = provider;
        DbContext = dbContext;
    }

    public AppDbContext DbContext { get; }

    public static async Task<RelationshipTestHost> CreateAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var databaseName = $"relationship-{Guid.NewGuid()}";

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

        services.AddCareRelationshipServices();
        services.AddScoped<IPatientDoctorService, PatientDoctorService>();
        services.AddScoped<IDoctorPatientService, DoctorPatientService>();
        services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();

        // Applied after defaults so tests can replace DbContext or other services.
        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedRolesAsync(provider);

        return new RelationshipTestHost(provider, dbContext);
    }

    public PatientDoctorsController CreatePatientDoctorsController(Guid? patientId = null) =>
        new(_provider.GetRequiredService<IPatientDoctorService>())
        {
            ControllerContext = CreateControllerContext(patientId, RoleNames.Patient),
        };

    public DoctorPatientsController CreateDoctorPatientsController(Guid? doctorId = null) =>
        new(_provider.GetRequiredService<IDoctorPatientService>())
        {
            ControllerContext = CreateControllerContext(doctorId, RoleNames.Doctor),
        };

    public DoctorDashboardController CreateDoctorDashboardController(Guid? doctorId = null) =>
        new(_provider.GetRequiredService<IDoctorDashboardService>())
        {
            ControllerContext = CreateControllerContext(doctorId, RoleNames.Doctor),
        };

    public T GetRequiredService<T>()
        where T : notnull =>
        _provider.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    public static ControllerContext CreateControllerContext(Guid? userId, string? role = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = claims.Count > 0
            ? new ClaimsIdentity(claims, authenticationType: "Test")
            : new ClaimsIdentity();

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
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

internal sealed record RelationshipScenario(
    ApplicationUser Patient,
    ApplicationUser Doctor,
    Clinic ClinicA,
    Clinic? ClinicB = null)
{
    public Guid ClinicAId => ClinicA.ClinicId;

    public Guid ClinicBId => ClinicB?.ClinicId ?? Guid.Empty;
}

internal static class RelationshipTestHostSeeder
{
    public static async Task<RelationshipScenario> SeedPatientDoctorClinicAsync(
        RelationshipTestHost host,
        bool includeSecondClinic = false,
        string clinicAName = "North Clinic",
        string? clinicBName = "South Clinic")
    {
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551000001");
        var doctor = ApplicationUserBuilder.Doctor(name: "Dr. Ahmadi", phoneNumber: "+15552000001");
        var profile = DoctorProfileBuilder.Create(
            doctor.Id,
            specialty: "Physiotherapy",
            medicalLicenseNumber: "MD-1001",
            clinicAddress: "123 Health St");

        var clinicA = ClinicBuilder.Create(
            ClinicBuilder.DefaultClinicId,
            managerId: doctor.Id,
            name: clinicAName,
            address: "North Address");
        var memberships = new List<ClinicDoctor>
        {
            ClinicBuilder.CreateMembership(clinicA.ClinicId, doctor.Id),
        };

        Clinic? clinicB = null;
        if (includeSecondClinic)
        {
            clinicB = ClinicBuilder.Create(managerId: doctor.Id, name: clinicBName!, address: "South Address");
            memberships.Add(ClinicBuilder.CreateMembership(clinicB.ClinicId, doctor.Id));
        }

        host.DbContext.Users.AddRange(patient, doctor);
        host.DbContext.DoctorProfiles.Add(profile);
        host.DbContext.Clinics.Add(clinicA);
        if (clinicB is not null)
        {
            host.DbContext.Clinics.Add(clinicB);
        }

        host.DbContext.ClinicDoctors.AddRange(memberships);

        var patientRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Patient);
        var doctorRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Doctor);
        host.DbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = patient.Id, RoleId = patientRole.Id },
            new IdentityUserRole<Guid> { UserId = doctor.Id, RoleId = doctorRole.Id });

        await host.DbContext.SaveChangesAsync();

        return new RelationshipScenario(patient, doctor, clinicA, clinicB);
    }

    public static async Task<ApplicationUser> SeedExtraDoctorAsync(
        RelationshipTestHost host,
        Clinic clinic,
        string name = "Dr. Other",
        string phoneNumber = "+15553000001")
    {
        var doctor = ApplicationUserBuilder.Doctor(name: name, phoneNumber: phoneNumber);
        host.DbContext.Users.Add(doctor);
        host.DbContext.DoctorProfiles.Add(DoctorProfileBuilder.Create(doctor.Id));
        host.DbContext.ClinicDoctors.Add(ClinicBuilder.CreateMembership(clinic.ClinicId, doctor.Id));

        var doctorRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Doctor);
        host.DbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = doctor.Id,
            RoleId = doctorRole.Id,
        });

        await host.DbContext.SaveChangesAsync();
        return doctor;
    }
}

/// <summary>
/// Simulates a persistence failure when saving DoctorPatient changes.
/// </summary>
internal sealed class FailingDoctorPatientSaveInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    public bool FailOnNextDoctorPatientSave { get; set; }

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
        if (!FailOnNextDoctorPatientSave || context is null)
        {
            return;
        }

        var hasDoctorPatientChange = context.ChangeTracker.Entries<DoctorPatient>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified);
        if (!hasDoctorPatientChange)
        {
            return;
        }

        FailOnNextDoctorPatientSave = false;
        throw new InvalidOperationException("Simulated DoctorPatient save failure.");
    }
}

internal static class RelationshipTestHostExtensions
{
    public static void UseFailingDoctorPatientSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingDoctorPatientSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"relationship-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }
}
