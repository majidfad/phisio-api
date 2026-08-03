namespace Phisio.Application.Admin.Patients;

public sealed class CreateAdminPatientDto
{
    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public bool GeneratePassword { get; set; }
}
