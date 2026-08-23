namespace Phisio.Application.Admin.Doctors;

public sealed class CreateAdminDoctorDto
{
    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Specialty { get; set; } = string.Empty;

    public string MedicalLicenseNumber { get; set; } = string.Empty;

    public string ClinicAddress { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public bool GeneratePassword { get; set; }

    /// <summary>Required. Used to find an existing clinic or create a new one.</summary>
    public IList<string> ClinicPhoneNumbers { get; set; } = [];

    /// <summary>Required when no clinic matches <see cref="ClinicPhoneNumbers"/>.</summary>
    public string? NewClinicName { get; set; }

    /// <summary>Required when no clinic matches <see cref="ClinicPhoneNumbers"/>.</summary>
    public string? NewClinicAddress { get; set; }

    /// <summary>
    /// When creating a clinic, sets the new doctor as <c>ClinicManagerId</c>.
    /// </summary>
    public bool ManagerIsThisDoctor { get; set; }

    /// <summary>
    /// Manager for a new clinic when <see cref="ManagerIsThisDoctor"/> is false.
    /// </summary>
    public Guid? ClinicManagerId { get; set; }
}
