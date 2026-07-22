using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Tests.TestDataBuilder;

public static class DoctorPatientBuilder
{
    public static DoctorPatient Create(
        Guid doctorId,
        Guid patientId,
        DateTime? createdAt = null,
        bool isEnabled = true,
        DoctorPatientStatus status = DoctorPatientStatus.Approved)
    {
        return new DoctorPatient
        {
            DoctorId = doctorId,
            PatientId = patientId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            IsEnabled = isEnabled,
            Status = status,
        };
    }
}
