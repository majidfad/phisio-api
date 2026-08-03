using Phisio.Application.Doctors;

namespace Phisio.Application.Admin.Doctors;

public sealed record CreateAdminDoctorResponse(DoctorDto Doctor, string? GeneratedPassword = null);
