using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class DoctorProfile : BaseEntity
{
    public Guid DoctorProfileId { get; set; }

    public Guid DoctorId { get; set; }

    public string Specialty { get; set; } = string.Empty;

    public string MedicalLicenseNumber { get; set; } = string.Empty;

    public string ClinicAddress { get; set; } = string.Empty;
}
