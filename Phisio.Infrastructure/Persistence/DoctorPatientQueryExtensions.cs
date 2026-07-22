using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Infrastructure.Persistence;

public static class DoctorPatientQueryExtensions
{
    /// <summary>Approved and enabled care relationships.</summary>
    public static IQueryable<DoctorPatient> WhereActive(this IQueryable<DoctorPatient> query) =>
        query.Where(dp => dp.IsEnabled && dp.Status == DoctorPatientStatus.Approved);

    public static IQueryable<DoctorPatient> WherePending(this IQueryable<DoctorPatient> query) =>
        query.Where(dp => dp.IsEnabled && dp.Status == DoctorPatientStatus.Pending);
}
