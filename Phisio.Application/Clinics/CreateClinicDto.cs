namespace Phisio.Application.Clinics;

public sealed class CreateClinicDto
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public IList<string> PhoneNumbers { get; set; } = [];

    /// <summary>Admin only. Ignored for clinic managers; their own user id is used instead.</summary>
    public Guid? ClinicManagerId { get; set; }
}
