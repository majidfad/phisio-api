using FluentValidation;
using Phisio.Domain.Enums;

namespace Phisio.Application.PatientVisits.Validators;

public class RegisterPatientVisitRequestValidator : AbstractValidator<RegisterPatientVisitRequest>
{
    public RegisterPatientVisitRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.ClinicId).NotEmpty();

        RuleFor(x => x.VisitAt).NotEqual(default(DateTime));

        RuleFor(x => x.VisitType)
            .IsInEnum()
            .When(x => x.VisitType is not null);

        RuleFor(x => x.PatientCondition)
            .IsInEnum()
            .When(x => x.PatientCondition is not null);

        RuleFor(x => x.DoctorNotes)
            .MaximumLength(2000)
            .When(x => x.DoctorNotes is not null);
    }
}

