using FluentValidation;

namespace Phisio.Application.Admin.Doctors.Validators;

public class CreateAdminDoctorDtoValidator : AbstractValidator<CreateAdminDoctorDto>
{
    public CreateAdminDoctorDtoValidator()
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

        RuleFor(x => x.Specialty)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MedicalLicenseNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.ClinicAddress)
            .NotEmpty()
            .MaximumLength(500);
    }
}

public class UpdateAdminDoctorDtoValidator : AbstractValidator<UpdateAdminDoctorDto>
{
    public UpdateAdminDoctorDtoValidator()
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

        RuleFor(x => x.Specialty)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MedicalLicenseNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.ClinicAddress)
            .NotEmpty()
            .MaximumLength(500);
    }
}
