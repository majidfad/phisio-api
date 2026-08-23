namespace Phisio.Application.Clinics;

public static class ClinicPhoneLookupStatus
{
    public const string None = "None";
    public const string Found = "Found";
    public const string Conflict = "Conflict";
}

public sealed record ClinicPhoneLookupResultDto(
    string Status,
    ClinicDto? Clinic,
    IReadOnlyList<ClinicDto> ConflictingClinics);
