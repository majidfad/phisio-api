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
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.ClinicAddress));

        RuleFor(x => x.NewClinicAddress)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.NewClinicAddress));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required when generate password is not selected.")
            .When(x => !x.GeneratePassword);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm password is required when generate password is not selected.")
            .Equal(x => x.Password)
            .WithMessage("Password and confirmation do not match.")
            .When(x => !x.GeneratePassword);

        RuleFor(x => x.ClinicPhoneNumbers)
            .NotEmpty()
            .WithMessage("At least one clinic phone number is required.");

        RuleForEach(x => x.ClinicPhoneNumbers)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("Clinic phone number format is invalid.");
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
