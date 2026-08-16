namespace Phisio.Application.Common;

public static class AuthorizationPolicies
{
    public const string DoctorOnly = nameof(DoctorOnly);

    public const string PatientOnly = nameof(PatientOnly);

    public const string AdminOnly = nameof(AdminOnly);

    public const string ClinicManagement = nameof(ClinicManagement);
}
