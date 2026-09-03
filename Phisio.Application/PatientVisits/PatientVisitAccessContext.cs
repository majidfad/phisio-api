using Phisio.Domain.Enums;

namespace Phisio.Application.PatientVisits;

public sealed record PatientVisitAccessContext(Guid UserId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsClinicManager => Role == UserRole.ClinicManager;
    public bool IsDoctor => Role == UserRole.Doctor;
    public bool IsPatient => Role == UserRole.Patient;
}

