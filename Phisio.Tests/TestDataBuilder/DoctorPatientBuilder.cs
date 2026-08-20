using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Tests.TestDataBuilder;

public static class DoctorPatientBuilder
{
    public static readonly Guid DefaultClinicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static DoctorPatient Create(
        Guid doctorId,
        Guid patientId,
        Guid? clinicId = null,
        DateTime? createdAt = null,
        bool isEnabled = true,
        DoctorPatientStatus status = DoctorPatientStatus.Approved)
    {
        return new DoctorPatient
        {
            DoctorId = doctorId,
            PatientId = patientId,
            ClinicId = clinicId ?? DefaultClinicId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            IsEnabled = isEnabled,
            Status = status,
        };
    }
}
