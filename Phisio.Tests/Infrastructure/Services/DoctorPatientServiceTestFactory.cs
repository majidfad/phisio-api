using Moq;
using Phisio.Application.CareDelivery;
using Phisio.Application.CarePlans;
using Phisio.Application.Common;
using Phisio.Application.ReadModels;
using Phisio.Application.Relationships;
using Phisio.Infrastructure.Events;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;

namespace Phisio.Tests.Infrastructure.Services;

internal static class DoctorPatientServiceTestFactory
{
    public static DoctorPatientService Create(
        AppDbContext dbContext,
        ICareRelationshipService? careRelationships = null,
        IDomainEventDispatcher? domainEvents = null,
        IPatientCareAssignmentService? assignments = null,
        IExerciseProgramService? programs = null,
        IPatientCareQueryService? queries = null)
    {
        domainEvents ??= NoOpDomainEventDispatcher.Instance;
        careRelationships ??= new CareRelationshipService(dbContext, domainEvents);
        assignments ??= new PatientCareAssignmentService(dbContext, careRelationships, domainEvents);
        programs ??= new ExerciseProgramService(dbContext, careRelationships, domainEvents);
        queries ??= new PatientCareQueryService(dbContext, careRelationships, programs);

        return new DoctorPatientService(careRelationships, assignments, programs, queries);
    }

    public static AssignmentService CreateAssignmentService(AppDbContext dbContext)
    {
        var domainEvents = NoOpDomainEventDispatcher.Instance;
        var careRelationships = new CareRelationshipService(dbContext, domainEvents);
        return new AssignmentService(dbContext, careRelationships);
    }

    public static ICareRelationshipService CreateCareRelationshipMock(bool hasActiveRelationship = true)
    {
        var mock = new Mock<ICareRelationshipService>();
        mock.Setup(service => service.HasActiveRelationshipAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasActiveRelationship);
        mock.Setup(service => service.EnsureCareAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                hasActiveRelationship
                    ? AuthResult<bool>.Success(true)
                    : AuthResult<bool>.Failure(["Patient not found."]));
        return mock.Object;
    }
}
