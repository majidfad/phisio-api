using FluentValidation;

namespace Phisio.Application.Admin.Validators;

public class AdminSetPasswordRequestValidator : AbstractValidator<AdminSetPasswordRequest>
{
    public AdminSetPasswordRequestValidator()
    {
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
    }
}
