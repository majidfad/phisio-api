namespace Phisio.Application.Clinics;

public sealed record ClinicDto(
    Guid ClinicId,
    string Name,
    string Address,
    Guid ClinicManagerId,
    IReadOnlyList<string> PhoneNumbers,
    DateTime CreatedAt,
    bool IsEnabled = true);
