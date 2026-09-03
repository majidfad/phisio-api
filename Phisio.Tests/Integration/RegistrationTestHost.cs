using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phisio.Api.Controllers;
using Phisio.Application.Auth;
using Phisio.Application.Auth.Validators;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;

namespace Phisio.Tests.Integration;

/// <summary>
/// Shared end-to-end registration harness: real Identity + EF InMemory + Auth/Clinic services,
/// exercised through the real API controllers.
/// </summary>
internal sealed class RegistrationTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private RegistrationTestHost(ServiceProvider provider, AppDbContext dbContext, AuthController authController)
    {
        _provider = provider;
        DbContext = dbContext;
        AuthController = authController;
    }

    public AppDbContext DbContext { get; }

    public AuthController AuthController { get; }

    public static async Task<RegistrationTestHost> CreateAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var databaseName = $"registration-{Guid.NewGuid()}";

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.Configure<JwtSettings>(settings =>
        {
            settings.Issuer = "Phisio.Api.Tests";
            settings.Audience = "Phisio.Client.Tests";
            settings.SecretKey = "integration-test-secret-key-minimum-32-characters";
            settings.AccessTokenExpirationMinutes = 60;
        });

        services.AddScoped<IClinicService, ClinicService>();
        services.AddScoped<IAuthService, AuthService>();

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedRolesAsync(provider);

        var authService = provider.GetRequiredService<IAuthService>();
        var clinicService = provider.GetRequiredService<IClinicService>();
        var authController = new AuthController(authService, clinicService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return new RegistrationTestHost(provider, dbContext, authController);
    }

    /// <summary>
    /// Mirrors ASP.NET FluentValidation auto-validation, then invokes the controller action.
    /// </summary>
    public async Task<IActionResult> RegisterPatientValidatedAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new RegisterPatientRequestValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await AuthController.RegisterPatient(request, cancellationToken);
    }

    /// <summary>
    /// Mirrors ASP.NET FluentValidation auto-validation, then invokes the controller action.
    /// </summary>
    public async Task<IActionResult> RegisterValidatedAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new RegisterRequestValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await AuthController.Register(request, cancellationToken);
    }

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

internal sealed class AssignFailingClinicService : IClinicService
{
    private readonly IClinicService _inner;

    public AssignFailingClinicService(IClinicService inner)
    {
        _inner = inner;
    }

    public Task<AuthResult<IReadOnlyList<ClinicDto>>> GetAllAsync(
        ClinicAccessContext access,
        bool isEnabled = true,
        CancellationToken cancellationToken = default) =>
        _inner.GetAllAsync(access, isEnabled, cancellationToken);

    public Task<AuthResult<ClinicDto>> GetByIdAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(access, clinicId, cancellationToken);

    public Task<AuthResult<ClinicDto>> CreateAsync(
        ClinicAccessContext access,
        CreateClinicDto request,
        CancellationToken cancellationToken = default) =>
        _inner.CreateAsync(access, request, cancellationToken);

    public Task<AuthResult<ClinicDto>> UpdateAsync(
        ClinicAccessContext access,
        Guid clinicId,
        UpdateClinicDto request,
        CancellationToken cancellationToken = default) =>
        _inner.UpdateAsync(access, clinicId, request, cancellationToken);

    public Task<AuthResult<ClinicDto>> ChangeManagerAsync(
        ClinicAccessContext access,
        Guid clinicId,
        ChangeClinicManagerDto request,
        CancellationToken cancellationToken = default) =>
        _inner.ChangeManagerAsync(access, clinicId, request, cancellationToken);

    public Task<AuthResult<bool>> DeleteAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(access, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>> GetDoctorsAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _inner.GetDoctorsAsync(access, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<ClinicPatientDto>>> GetPatientsAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetPatientsAsync(access, clinicId, doctorId, cancellationToken);

    public Task<AuthResult<ClinicAdherenceResponse>> GetAdherenceAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetAdherenceAsync(access, clinicId, doctorId, cancellationToken);

    public Task<AuthResult<ClinicDoctorMemberDto>> AddDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default) =>
        _inner.AddDoctorAsync(access, clinicId, doctorId, cancellationToken);

    public Task<AuthResult<bool>> RemoveDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default) =>
        _inner.RemoveDoctorAsync(access, clinicId, doctorId, cancellationToken);

    public Task<AuthResult<ClinicPhoneLookupResultDto>> LookupByPhonesAsync(
        ClinicAccessContext access,
        LookupClinicsByPhonesDto request,
        CancellationToken cancellationToken = default) =>
        _inner.LookupByPhonesAsync(access, request, cancellationToken);

    public Task<AuthResult<AssignDoctorToClinicResultDto>> AssignDoctorAsync(
        ClinicAccessContext access,
        AssignDoctorToClinicDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            AuthResult<AssignDoctorToClinicResultDto>.Failure(
                ["Simulated clinic assignment failure."]));
}

/// <summary>
/// Forces AddToRoleAsync to fail so patient registration rollback can be verified.
/// </summary>
internal sealed class AddToRoleFailingUserManager : UserManager<ApplicationUser>
{
    public AddToRoleFailingUserManager(
        IUserStore<ApplicationUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<ApplicationUser>> logger)
        : base(
            store,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger)
    {
    }

    public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role) =>
        Task.FromResult(
            IdentityResult.Failed(new IdentityError
            {
                Code = "SimulatedRoleFailure",
                Description = "Simulated role assignment failure.",
            }));
}

internal static class RegistrationTestHostExtensions
{
    public static void UseFailingAddToRoleUserManager(this IServiceCollection services)
    {
        services.RemoveAll<UserManager<ApplicationUser>>();
        services.AddScoped<UserManager<ApplicationUser>, AddToRoleFailingUserManager>();
    }
}
