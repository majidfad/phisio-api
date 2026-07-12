namespace Phisio.Application.Patients;

public sealed record PatientDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    DateTime FirstAssignedAt,
    string? Email = null,
    DateTime? CreatedAt = null,
    IReadOnlyList<string> DoctorNames = null!,
    bool IsEnabled = true);
