using Phisio.Domain.Enums;

namespace Phisio.Application.Common;

public static class RoleNames
{
    public const string Doctor = nameof(UserRole.Doctor);

    public const string ClinicManager = nameof(UserRole.ClinicManager);

    public const string Patient = nameof(UserRole.Patient);

    public const string Admin = nameof(UserRole.Admin);

    /// <summary>
    /// Identity role names that may access doctor-scoped API endpoints.
    /// </summary>
    public static readonly string[] DoctorAccess = [Doctor, ClinicManager];
}

public static class UserRoleExtensions
{
    public static bool HasDoctorAccess(this UserRole role) =>
        role is UserRole.Doctor or UserRole.ClinicManager;
}
