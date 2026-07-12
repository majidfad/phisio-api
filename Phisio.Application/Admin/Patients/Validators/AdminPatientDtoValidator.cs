using FluentValidation;

namespace Phisio.Application.Admin.Patients.Validators;

public class CreateAdminPatientDtoValidator : AbstractValidator<CreateAdminPatientDto>
{
    public CreateAdminPatientDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("Phone number format is invalid.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateAdminPatientDtoValidator : AbstractValidator<UpdateAdminPatientDto>
{
    public UpdateAdminPatientDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("Phone number format is invalid.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
