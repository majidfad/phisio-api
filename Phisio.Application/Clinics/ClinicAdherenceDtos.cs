namespace Phisio.Application.Clinics;

public sealed record ClinicAdherencePeriodDto(
    DateOnly From,
    DateOnly To,
    int ScheduledDays,
    int CompletedDays,
    int MissedDays,
    int AdherencePercentage);

public sealed record ClinicPatientAdherenceDto(
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    int ScheduledDays,
    int CompletedDays,
    int AdherencePercentage);

public sealed record ClinicAdherenceResponse(
    ClinicAdherencePeriodDto Today,
    ClinicAdherencePeriodDto Last7Days,
    ClinicAdherencePeriodDto Last30Days,
    IReadOnlyList<ClinicPatientAdherenceDto> Patients);
