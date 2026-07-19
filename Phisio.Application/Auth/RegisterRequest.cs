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
}
