namespace Phisio.Application.Clinics;

public static class ClinicErrors
{
    public const string NotFound = "Clinic not found.";

    public const string ManagerIdRequired = "ClinicManagerId is required.";

    public const string ManagerNotFound = "Clinic manager not found.";

    public const string ManagerMustBeDoctor = "The selected clinic manager must be an existing doctor.";

    public const string ManagerRoleNotConfigured = "The ClinicManager role is not configured.";

    public const string PhoneNumberRequired = "At least one clinic phone number is required.";

    public const string PhoneNumberAlreadyExists =
        "A clinic with one of the submitted phone numbers already exists.";

    public const string DoctorNotFound = "Doctor not found.";

    public const string DoctorAlreadyAssigned = "Doctor is already assigned to this clinic.";

    public const string DoctorCannotBeAssigned = "The specified user cannot be assigned as a clinic doctor.";

    public const string CannotRemoveClinicManager = "The clinic manager cannot be removed from the clinic.";

    public const string ConflictingClinicPhones =
        "The entered phone numbers belong to different clinics. Correct the numbers and try again.";

    public const string ClinicCreateDetailsRequired =
        "Clinic name and address are required when no existing clinic matches the phone numbers.";
}
