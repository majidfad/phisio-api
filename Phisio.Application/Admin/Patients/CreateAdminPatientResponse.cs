using Phisio.Application.Patients;

namespace Phisio.Application.Admin.Patients;

public sealed record CreateAdminPatientResponse(PatientDto Patient, string? GeneratedPassword = null);
