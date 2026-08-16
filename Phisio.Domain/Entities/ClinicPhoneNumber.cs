namespace Phisio.Domain.Entities;

public class ClinicPhoneNumber
{
    public Guid ClinicPhoneNumberId { get; set; }

    public Guid ClinicId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string NormalizedPhoneNumber { get; set; } = string.Empty;

    public Clinic Clinic { get; set; } = null!;
}
