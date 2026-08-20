using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

public static class ClinicBuilder
{
    public static readonly Guid DefaultClinicId = DoctorPatientBuilder.DefaultClinicId;

    public static Clinic CreateDefault(Guid? managerId = null) =>
        Create(DefaultClinicId, managerId);

    public static Clinic Create(
        Guid? clinicId = null,
        Guid? managerId = null,
        string name = "Test Clinic",
        string address = "Test Address",
        bool isEnabled = true)
    {
        var id = clinicId ?? Guid.NewGuid();
        var manager = managerId ?? Guid.NewGuid();

        return new Clinic
        {
            ClinicId = id,
            Name = name,
            Address = address,
            ClinicManagerId = manager,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static ClinicDoctor CreateMembership(Guid clinicId, Guid doctorId) =>
        new()
        {
            ClinicId = clinicId,
            DoctorId = doctorId,
        };
}
