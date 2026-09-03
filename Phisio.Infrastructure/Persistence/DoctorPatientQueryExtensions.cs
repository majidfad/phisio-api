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

    /// <summary>
    /// Open care links: approved or pending and not soft-deleted.
    /// A patient may have at most one such link at a time.
    /// </summary>
    public static IQueryable<DoctorPatient> WhereOpenCare(this IQueryable<DoctorPatient> query) =>
        query.Where(dp =>
            dp.IsEnabled
            && (dp.Status == DoctorPatientStatus.Approved || dp.Status == DoctorPatientStatus.Pending));

    public static IQueryable<DoctorPatient> WhereClinic(
        this IQueryable<DoctorPatient> query,
        Guid? clinicId) =>
        clinicId is null ? query : query.Where(dp => dp.ClinicId == clinicId.Value);
}
