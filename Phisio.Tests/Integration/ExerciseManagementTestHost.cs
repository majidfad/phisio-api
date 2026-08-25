using System.Security.Claims;
using FluentValidation;
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
using Phisio.Api.Controllers.Admin;
using Phisio.Api.Controllers.Doctor;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Admin.Exercises.Validators;
using Phisio.Application.Assignments;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;
using Phisio.Application.DoctorPatients;
using Phisio.Application.DoctorPatients.Validators;
using Phisio.Application.Exercises;
using Phisio.Application.Exercises.Validators;
using Phisio.Application.Notifications;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.PatientDailyFeedback.Validators;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end harness for exercise catalog and assignment workflows:
/// real EF InMemory + exercise/assignment services, exercised through API controllers.
/// </summary>
internal sealed class ExerciseManagementTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ExerciseManagementTestHost(ServiceProvider provider, AppDbContext dbContext)
    {
        _provider = provider;
        DbContext = dbContext;
    }

    public AppDbContext DbContext { get; }

    public static async Task<ExerciseManagementTestHost> CreateAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var databaseName = $"exercise-mgmt-{Guid.NewGuid()}";

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

        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IDoctorPatientService, DoctorPatientService>();
        services.AddScoped<IPatientExerciseService, PatientExerciseService>();
        services.AddScoped<IDoctorExerciseService, DoctorExerciseService>();
        services.AddScoped<IPatientDailyFeedbackService, PatientDailyFeedbackService>();

        services.AddSingleton<RecordingNotificationService>();
        services.AddSingleton<INotificationService>(sp =>
            sp.GetRequiredService<RecordingNotificationService>());

        var videoUpload = new Mock<IExerciseVideoUploadService>();
        services.AddSingleton(videoUpload.Object);

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedRolesAsync(provider);

        return new ExerciseManagementTestHost(provider, dbContext);
    }

    public AdminExercisesController CreateAdminExercisesController(Guid? userId, params string[] roles) =>
        new(
            _provider.GetRequiredService<IExerciseService>(),
            _provider.GetRequiredService<IExerciseVideoUploadService>())
        {
            ControllerContext = CreateControllerContext(userId, roles),
        };

    public AdminExercisesController CreateAdminExercisesController(Guid adminId) =>
        CreateAdminExercisesController(adminId, RoleNames.Admin);

    public AssignmentsController CreateAssignmentsController(Guid? userId, params string[] roles) =>
        new(_provider.GetRequiredService<IAssignmentService>())
        {
            ControllerContext = CreateControllerContext(userId, roles),
        };

    public DoctorPatientsController CreateDoctorPatientsController(Guid? userId, params string[] roles)
    {
        var effectiveRoles = roles.Length == 0 && userId is not null
            ? [RoleNames.Doctor]
            : roles;

        return new(_provider.GetRequiredService<IDoctorPatientService>())
        {
            ControllerContext = CreateControllerContext(userId, effectiveRoles),
        };
    }

    public PatientExercisesController CreatePatientExercisesController(Guid? userId, params string[] roles)
    {
        var effectiveRoles = roles.Length == 0 && userId is not null
            ? [RoleNames.Patient]
            : roles;

        return new(_provider.GetRequiredService<IPatientExerciseService>())
        {
            ControllerContext = CreateControllerContext(userId, effectiveRoles),
        };
    }

    public PatientDailyFeedbackController CreatePatientDailyFeedbackController(Guid? userId, params string[] roles)
    {
        var effectiveRoles = roles.Length == 0 && userId is not null
            ? [RoleNames.Patient]
            : roles;

        return new(_provider.GetRequiredService<IPatientDailyFeedbackService>())
        {
            ControllerContext = CreateControllerContext(userId, effectiveRoles),
        };
    }

    public AdminAssignmentsController CreateAdminAssignmentsController(Guid? userId, params string[] roles) =>
        new(_provider.GetRequiredService<IAssignmentService>())
        {
            ControllerContext = CreateControllerContext(userId, roles),
        };

    public T GetRequiredService<T>()
        where T : notnull =>
        _provider.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    public static ControllerContext CreateControllerContext(Guid? userId, params string[] roles)
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

internal sealed record ExerciseScenario(
    ApplicationUser Admin,
    ApplicationUser Doctor,
    ApplicationUser Patient,
    ApplicationUser OtherDoctor,
    ApplicationUser OtherPatient,
    Clinic ClinicA,
    Clinic? ClinicB,
    Exercise AdminExercise,
    Exercise DoctorExercise,
    Exercise DoctorExercise2)
{
    public Guid ClinicAId => ClinicA.ClinicId;

    public Guid ClinicBId => ClinicB?.ClinicId ?? Guid.Empty;
}

internal static class ExerciseManagementTestHostSeeder
{
    public static async Task<ExerciseScenario> SeedFullScenarioAsync(
        ExerciseManagementTestHost host,
        bool includeSecondClinic = false)
    {
        var admin = ApplicationUserBuilder.Admin(phoneNumber: "+15550000001");
        var doctor = ApplicationUserBuilder.Doctor(name: "Dr. Assigner", phoneNumber: "+15552000001");
        var patient = ApplicationUserBuilder.Patient(name: "Alice Patient", phoneNumber: "+15551000001");
        var otherDoctor = ApplicationUserBuilder.Doctor(name: "Dr. Other", phoneNumber: "+15552000002");
        var otherPatient = ApplicationUserBuilder.Patient(name: "Bob Patient", phoneNumber: "+15551000002");

        var clinicA = ClinicBuilder.Create(managerId: doctor.Id, name: "North Clinic", address: "North Address");
        var clinicDoctors = new List<ClinicDoctor>
        {
            ClinicBuilder.CreateMembership(clinicA.ClinicId, doctor.Id),
            ClinicBuilder.CreateMembership(clinicA.ClinicId, otherDoctor.Id),
        };

        Clinic? clinicB = null;
        if (includeSecondClinic)
        {
            clinicB = ClinicBuilder.Create(managerId: doctor.Id, name: "South Clinic", address: "South Address");
            clinicDoctors.Add(ClinicBuilder.CreateMembership(clinicB.ClinicId, doctor.Id));
        }

        host.DbContext.Users.AddRange(admin, doctor, patient, otherDoctor, otherPatient);
        host.DbContext.DoctorProfiles.AddRange(
            DoctorProfileBuilder.Create(doctor.Id),
            DoctorProfileBuilder.Create(otherDoctor.Id));
        host.DbContext.Clinics.Add(clinicA);
        if (clinicB is not null)
        {
            host.DbContext.Clinics.Add(clinicB);
        }

        host.DbContext.ClinicDoctors.AddRange(clinicDoctors);
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            doctor.Id,
            patient.Id,
            clinicA.ClinicId,
            status: DoctorPatientStatus.Approved));

        if (clinicB is not null)
        {
            host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
                doctor.Id,
                patient.Id,
                clinicB.ClinicId,
                status: DoctorPatientStatus.Approved));
        }

        var adminExercise = ExerciseBuilder.Create(title: "Admin Catalog Stretch");
        var doctorExercise = ExerciseBuilder.Create(
            title: "Doctor Custom Stretch",
            createdByDoctorId: doctor.Id);
        var doctorExercise2 = ExerciseBuilder.Create(
            title: "Doctor Bridge",
            createdByDoctorId: doctor.Id);

        host.DbContext.Exercises.AddRange(adminExercise, doctorExercise, doctorExercise2);

        var adminRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var doctorRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Doctor);
        var patientRole = await host.DbContext.Roles.SingleAsync(role => role.Name == RoleNames.Patient);

        host.DbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = doctor.Id, RoleId = doctorRole.Id },
            new IdentityUserRole<Guid> { UserId = patient.Id, RoleId = patientRole.Id },
            new IdentityUserRole<Guid> { UserId = otherDoctor.Id, RoleId = doctorRole.Id },
            new IdentityUserRole<Guid> { UserId = otherPatient.Id, RoleId = patientRole.Id });

        await host.DbContext.SaveChangesAsync();

        return new ExerciseScenario(
            admin,
            doctor,
            patient,
            otherDoctor,
            otherPatient,
            clinicA,
            clinicB,
            adminExercise,
            doctorExercise,
            doctorExercise2);
    }

    public static async Task<ApplicationUser> SeedAdminAsync(ExerciseManagementTestHost host)
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

    public static async Task SeedDoctorPatientLinkAsync(
        ExerciseManagementTestHost host,
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        DoctorPatientStatus status = DoctorPatientStatus.Approved)
    {
        host.DbContext.DoctorPatients.Add(DoctorPatientBuilder.Create(
            doctorId,
            patientId,
            clinicId,
            status: status));
        await host.DbContext.SaveChangesAsync();
    }

    public static async Task<UserExercise> SeedAssignmentAsync(
        ExerciseManagementTestHost host,
        Guid doctorId,
        Guid patientId,
        Guid exerciseId,
        DateOnly? scheduledDate = null,
        int? sets = 3,
        string? reps = "10",
        string? patientCue = "Breathe",
        string? clinicianNote = "Keep form",
        Guid? programId = null,
        bool isActive = true,
        bool isEnabled = true)
    {
        var assignment = AssignmentBuilder.Create(
            doctorId,
            patientId,
            exerciseId,
            isActive: isActive,
            scheduledDate: scheduledDate ?? ExerciseManagementTestHelpers.Today);
        assignment.Sets = sets;
        assignment.Reps = reps;
        assignment.PatientCue = patientCue;
        assignment.ClinicianNote = clinicianNote;
        assignment.ProgramId = programId;
        assignment.IsEnabled = isEnabled;

        host.DbContext.UserExercises.Add(assignment);
        await host.DbContext.SaveChangesAsync();
        return assignment;
    }

    public static async Task<DailyPatientFeedback> SeedFeedbackAsync(
        ExerciseManagementTestHost host,
        Guid patientId,
        Guid doctorId,
        int improvementScore = 3,
        int hardnessScore = 3,
        string? comment = "Existing",
        DateOnly? feedbackDate = null,
        bool isEnabled = true)
    {
        var feedback = DailyPatientFeedbackBuilder.Create(
            patientId,
            doctorId,
            improvementScore: improvementScore,
            hardnessScore: hardnessScore,
            comment: comment,
            feedbackDate: feedbackDate ?? ExerciseManagementTestHelpers.Today,
            isEnabled: isEnabled);
        host.DbContext.DailyPatientFeedbacks.Add(feedback);
        await host.DbContext.SaveChangesAsync();
        return feedback;
    }
}

internal static class ExerciseManagementTestHelpers
{
    public static async Task<IActionResult> CreateExerciseWithValidationAsync(
        AdminExercisesController controller,
        CreateExerciseDto request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new CreateExerciseDtoValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await controller.CreateExercise(request, cancellationToken);
    }

    public static async Task<IActionResult> UpdateExerciseWithValidationAsync(
        AdminExercisesController controller,
        Guid exerciseId,
        UpdateExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new UpdateExerciseRequestValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await controller.UpdateExercise(exerciseId, request, cancellationToken);
    }

    public static CreateAssignmentRequest CreateAssignmentRequest(Guid patientId, Guid exerciseId) =>
        new()
        {
            PatientId = patientId,
            ExerciseId = exerciseId,
        };

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public const int EveryDayMask = 0b1111111;

    public static AssignPatientExerciseItem ProgramItem(
        Guid exerciseId,
        int? sets = 3,
        string? reps = "10",
        string? clinicianNote = "Keep form",
        string? patientCue = "Breathe") =>
        new(exerciseId, Sets: sets, Reps: reps, ClinicianNote: clinicianNote, PatientCue: patientCue);

    public static CreateExerciseProgramRequest DailyProgramRequest(
        DateOnly startDate,
        DateOnly endDate,
        params AssignPatientExerciseItem[] items) =>
        new(
            startDate,
            endDate,
            ExerciseProgramCadenceType.DaysOfWeek,
            EveryDayMask,
            IntervalDays: null,
            items);

    public static CreateExerciseProgramRequest IntervalProgramRequest(
        DateOnly startDate,
        DateOnly endDate,
        int intervalDays,
        params AssignPatientExerciseItem[] items) =>
        new(
            startDate,
            endDate,
            ExerciseProgramCadenceType.Interval,
            DaysOfWeekMask: 0,
            IntervalDays: intervalDays,
            items);

    public static UpdateExerciseProgramRequest ToUpdateRequest(CreateExerciseProgramRequest request) =>
        new(
            request.StartDate,
            request.EndDate,
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays,
            request.Items);

    public static async Task<IActionResult> CreateProgramWithValidationAsync(
        DoctorPatientsController controller,
        Guid patientId,
        CreateExerciseProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new CreateExerciseProgramRequestValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await controller.CreatePatientProgram(patientId, request, cancellationToken);
    }

    public static async Task<IActionResult> UpdateProgramWithValidationAsync(
        DoctorPatientsController controller,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new UpdateExerciseProgramRequestValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await controller.UpdatePatientProgram(patientId, programId, request, cancellationToken);
    }

    public static IReadOnlyList<DateOnly> ExpectedScheduleDates(
        CreateExerciseProgramRequest request,
        DateOnly? fromInclusive = null)
    {
        var from = fromInclusive ?? Today;
        return ExerciseProgramSchedule.ExpandFrom(
            request.StartDate,
            request.EndDate,
            from,
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays);
    }

    public static SubmitDailyFeedbackRequest ValidFeedbackRequest(
        Guid? doctorId = null,
        int improvementScore = 4,
        int hardnessScore = 3,
        string? comment = "Feeling better today") =>
        new()
        {
            DoctorId = doctorId,
            ImprovementScore = improvementScore,
            HardnessScore = hardnessScore,
            Comment = comment,
        };

    public static async Task<IActionResult> SubmitFeedbackWithValidationAsync(
        PatientDailyFeedbackController controller,
        SubmitDailyFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await new SubmitDailyFeedbackRequestValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new
            {
                errors = validation.Errors.Select(error => error.ErrorMessage).ToArray(),
            });
        }

        return await controller.SubmitFeedback(request, cancellationToken);
    }
}

/// <summary>
/// Simulates persistence failure when saving program or related assignment changes.
/// </summary>
internal sealed class FailingProgramSaveInterceptor : SaveChangesInterceptor
{
    public bool FailOnNextProgramRelatedSave { get; set; }

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
        if (!FailOnNextProgramRelatedSave || context is null)
        {
            return;
        }

        var hasRelevantChange =
            context.ChangeTracker.Entries<ExerciseProgram>().Any(IsPending)
            || context.ChangeTracker.Entries<ProgramExercise>().Any(IsPending)
            || context.ChangeTracker.Entries<UserExercise>().Any(IsPending);

        if (!hasRelevantChange)
        {
            return;
        }

        FailOnNextProgramRelatedSave = false;
        throw new InvalidOperationException("Simulated program persistence failure.");
    }

    private static bool IsPending(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
}

/// <summary>
/// Simulates persistence failure when saving UserExercise changes.
/// </summary>
internal sealed class FailingUserExerciseSaveInterceptor : SaveChangesInterceptor
{
    public bool FailOnNextUserExerciseSave { get; set; }

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
        if (!FailOnNextUserExerciseSave || context is null)
        {
            return;
        }

        var hasUserExerciseChange = context.ChangeTracker.Entries<UserExercise>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (!hasUserExerciseChange)
        {
            return;
        }

        FailOnNextUserExerciseSave = false;
        throw new InvalidOperationException("Simulated UserExercise save failure.");
    }
}

internal static class ExerciseManagementTestHostExtensions
{
    public static void UseFailingUserExerciseSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingUserExerciseSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"exercise-mgmt-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }

    public static void UseFailingProgramSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingProgramSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"exercise-program-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }

    public static void UseFailingExerciseCompletionSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingExerciseCompletionSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"exercise-completion-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }

    public static void UseFailingDailyFeedbackSaveInterceptor(this IServiceCollection services)
    {
        var interceptor = new FailingDailyFeedbackSaveInterceptor();
        services.AddSingleton(interceptor);

        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseInMemoryDatabase($"daily-feedback-fail-{Guid.NewGuid()}")
                .AddInterceptors(interceptor));
    }
}

/// <summary>
/// Captures notification side effects for integration assertions.
/// </summary>
internal sealed class RecordingNotificationService : INotificationService
{
    public List<RecordedNotification> Notifications { get; } = [];

    public Task CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        Notifications.Add(new RecordedNotification(userId, type, title, body, data));
        return Task.CompletedTask;
    }

    public Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            Notifications.Add(new RecordedNotification(userId, type, title, body, data));
        }

        return Task.CompletedTask;
    }

    public Task<AuthResult<IReadOnlyList<NotificationDto>>> GetForUserAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<IReadOnlyList<NotificationDto>>.Success([]));

    public Task<AuthResult<UnreadCountDto>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<UnreadCountDto>.Success(new UnreadCountDto(0)));

    public Task<AuthResult<bool>> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<bool>.Success(true));

    public Task<AuthResult<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<int>.Success(0));
}

internal sealed record RecordedNotification(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    object? Data);

/// <summary>
/// Simulates persistence failure when saving ExerciseCompletion changes.
/// </summary>
internal sealed class FailingExerciseCompletionSaveInterceptor : SaveChangesInterceptor
{
    public bool FailOnNextCompletionSave { get; set; }

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
        if (!FailOnNextCompletionSave || context is null)
        {
            return;
        }

        var hasCompletionChange = context.ChangeTracker.Entries<ExerciseCompletion>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (!hasCompletionChange)
        {
            return;
        }

        FailOnNextCompletionSave = false;
        throw new InvalidOperationException("Simulated ExerciseCompletion save failure.");
    }
}

/// <summary>
/// Simulates persistence failure when saving DailyPatientFeedback changes.
/// </summary>
internal sealed class FailingDailyFeedbackSaveInterceptor : SaveChangesInterceptor
{
    public bool FailOnNextFeedbackSave { get; set; }

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
        if (!FailOnNextFeedbackSave || context is null)
        {
            return;
        }

        var hasFeedbackChange = context.ChangeTracker.Entries<DailyPatientFeedback>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (!hasFeedbackChange)
        {
            return;
        }

        FailOnNextFeedbackSave = false;
        throw new InvalidOperationException("Simulated DailyPatientFeedback save failure.");
    }
}
