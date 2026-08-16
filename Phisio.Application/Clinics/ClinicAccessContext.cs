namespace Phisio.Application.Clinics;

public sealed record ClinicAccessContext(Guid UserId, bool IsAdmin);
