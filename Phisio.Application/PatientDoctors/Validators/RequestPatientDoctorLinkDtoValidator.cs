using FluentValidation;

namespace Phisio.Application.PatientDoctors.Validators;

public class RequestPatientDoctorLinkDtoValidator : AbstractValidator<RequestPatientDoctorLinkDto>
{
    public RequestPatientDoctorLinkDtoValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
    }
}
