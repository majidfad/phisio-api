using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Application.Admin.Dashboard;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Admin.Patients;
using Phisio.Application.Articles;
using Phisio.Application.Assignments;
using Phisio.Application.Auth;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.DoctorExercises;
using Phisio.Application.DoctorPatients;
using Phisio.Application.ExerciseCategories;
using Phisio.Application.Exercises;
using Phisio.Application.Patients;
using Phisio.Application.Notifications;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.PatientDoctors;
using Phisio.Application.PatientExercises;
using Phisio.Application.PatientSettings;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Background;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Persistence.Seeding;
using Phisio.Infrastructure.Push;
using Phisio.Infrastructure.Services;

namespace Phisio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));
        services.Configure<ExerciseUploadOptions>(configuration.GetSection(ExerciseUploadOptions.SectionName));
        services.Configure<VapidSettings>(configuration.GetSection(VapidSettings.SectionName));

        var maxUploadBytes = configuration.GetValue<long?>(
                $"{ExerciseUploadOptions.SectionName}:MaxFileSizeBytes")
            ?? ExerciseUploadLimits.MaxFileSizeBytes;
        if (maxUploadBytes <= 0)
        {
            maxUploadBytes = ExerciseUploadLimits.MaxFileSizeBytes;
        }

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = maxUploadBytes;
        });
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = maxUploadBytes;
        });

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
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

        services.AddJwtAuthentication(configuration);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IDoctorPatientService, DoctorPatientService>();
        services.AddScoped<IPatientDoctorService, PatientDoctorService>();
        services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();
        services.AddScoped<IDoctorExerciseService, DoctorExerciseService>();
        services.AddScoped<IAdminPatientService, AdminPatientService>();
        services.AddScoped<IAdminDoctorService, AdminDoctorService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IExerciseCategoryService, ExerciseCategoryService>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IExerciseVideoUploadService, ExerciseVideoUploadService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IPatientExerciseService, PatientExerciseService>();
        services.AddScoped<IPatientDailyFeedbackService, PatientDailyFeedbackService>();
        services.AddScoped<IPatientSettingsService, PatientSettingsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        services.AddScoped<IWebPushSender, WebPushSender>();
        services.AddHostedService<ExerciseReminderBackgroundService>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
