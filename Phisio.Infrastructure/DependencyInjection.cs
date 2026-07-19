using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Application.Admin.Dashboard;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Admin.Patients;
using Phisio.Application.Assignments;
using Phisio.Application.Auth;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.DoctorExercises;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Exercises;
using Phisio.Application.Patients;
using Phisio.Application.PatientExercises;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Persistence.Seeding;
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
        services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();
        services.AddScoped<IDoctorExerciseService, DoctorExerciseService>();
        services.AddScoped<IAdminPatientService, AdminPatientService>();
        services.AddScoped<IAdminDoctorService, AdminDoctorService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IExerciseVideoUploadService, ExerciseVideoUploadService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IPatientExerciseService, PatientExerciseService>();
        services.AddScoped<IPatientDailyFeedbackService, PatientDailyFeedbackService>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
