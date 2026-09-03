namespace Phisio.Domain.Entities;

public class ClinicDoctor
{
    public Guid ClinicId { get; set; }

    public Guid DoctorId { get; set; }

    public Clinic Clinic { get; set; } = null!;
}
