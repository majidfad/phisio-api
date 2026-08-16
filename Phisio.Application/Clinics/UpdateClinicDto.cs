namespace Phisio.Application.Clinics;

public sealed class UpdateClinicDto
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public IList<string> PhoneNumbers { get; set; } = [];
}
