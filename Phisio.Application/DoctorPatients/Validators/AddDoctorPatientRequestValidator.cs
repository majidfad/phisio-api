using FluentValidation;

namespace Phisio.Application.DoctorPatients.Validators;

public class AddDoctorPatientRequestValidator : AbstractValidator<AddDoctorPatientRequest>
{
    public AddDoctorPatientRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ClinicId).NotEmpty();
    }
}
