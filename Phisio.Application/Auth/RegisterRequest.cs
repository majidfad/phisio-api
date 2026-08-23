using Phisio.Domain.Enums;

namespace Phisio.Application.Auth;

public sealed class RegisterRequest
{
    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Patient;

    /// <summary>Required when registering as a doctor.</summary>
    public string? MedicalLicenseNumber { get; set; }

    /// <summary>Required when registering as a doctor.</summary>
    public string? Specialty { get; set; }

    /// <summary>Required when registering as a doctor. Used to find or create a clinic.</summary>
    public IList<string> ClinicPhoneNumbers { get; set; } = [];

    /// <summary>Required when no clinic matches <see cref="ClinicPhoneNumbers"/>.</summary>
    public string? NewClinicName { get; set; }

    /// <summary>Required when no clinic matches <see cref="ClinicPhoneNumbers"/>.</summary>
    public string? NewClinicAddress { get; set; }

    /// <summary>
    /// When creating a clinic during registration, sets the new doctor as <c>ClinicManagerId</c>.
    /// </summary>
    public bool ManagerIsThisDoctor { get; set; }
}
