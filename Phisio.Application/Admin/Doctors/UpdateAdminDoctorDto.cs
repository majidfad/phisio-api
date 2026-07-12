namespace Phisio.Application.Admin.Doctors;

public sealed class UpdateAdminDoctorDto
{
    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Specialty { get; set; } = string.Empty;

    public string MedicalLicenseNumber { get; set; } = string.Empty;

    public string ClinicAddress { get; set; } = string.Empty;
}
