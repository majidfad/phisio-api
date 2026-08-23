namespace Phisio.Application.Clinics;

public sealed class AssignDoctorToClinicDto
{
    public Guid DoctorId { get; set; }

    public IList<string> PhoneNumbers { get; set; } = [];

    /// <summary>Required when no existing clinic matches the phone numbers.</summary>
    public string? Name { get; set; }

    /// <summary>Required when no existing clinic matches the phone numbers.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// When true and creating a clinic, the doctor being assigned becomes the clinic manager.
    /// </summary>
    public bool ManagerIsThisDoctor { get; set; }

    /// <summary>
    /// Admin-selected manager when creating a clinic and <see cref="ManagerIsThisDoctor"/> is false.
    /// </summary>
    public Guid? ClinicManagerId { get; set; }

    /// <summary>
    /// When true, allows assigning a doctor who is not yet enabled (e.g. pending registration approval).
    /// </summary>
    public bool AllowDisabledDoctor { get; set; }
}
