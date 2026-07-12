using FluentValidation;

namespace Phisio.Application.DoctorPatients.Validators;

public class AddDoctorPatientRequestValidator : AbstractValidator<AddDoctorPatientRequest>
{
    public AddDoctorPatientRequestValidator()
    {
        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20);
    }
}
