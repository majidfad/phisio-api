using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence;

public static class DoctorPatientQueryExtensions
{
    public static IQueryable<DoctorPatient> WhereActive(this IQueryable<DoctorPatient> query) =>
        query.Where(dp => dp.IsEnabled);
}
