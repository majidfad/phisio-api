namespace Phisio.Application.Admin.Patients;

public sealed class UpdateAdminPatientDto
{
    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }
}
