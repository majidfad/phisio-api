using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phisio.Application.CareDelivery;
using Phisio.Application.CarePlans;
using Phisio.Application.Common;
using Phisio.Application.Notifications;
using Phisio.Application.ReadModels;
using Phisio.Application.Relationships;
using Phisio.Infrastructure.Events;
using Phisio.Infrastructure.Services;
using Phisio.Infrastructure.Services.ReadModels;

namespace Phisio.Tests.Integration;

internal static class CareServiceRegistration
{
    public static IServiceCollection AddCareRelationshipServices(this IServiceCollection services)
    {
        services.TryAddSingleton<RecordingNotificationService>();
        services.TryAddSingleton<INotificationService>(sp =>
            sp.GetRequiredService<RecordingNotificationService>());

        services.AddScoped<CareDomainEventNotificationHandler>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<ICareRelationshipService, CareRelationshipService>();
        services.AddScoped<IPatientCareAssignmentService, PatientCareAssignmentService>();
        services.AddScoped<IExerciseProgramService, ExerciseProgramService>();
        services.AddScoped<IPatientCareQueryService, PatientCareQueryService>();
        services.AddScoped<IDoctorDashboardReadService, DoctorDashboardReadService>();
        return services;
    }
}
