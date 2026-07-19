using FluentValidation;

namespace Phisio.Application.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("رمز عبور فعلی الزامی است.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("رمز عبور جدید الزامی است.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("تکرار رمز عبور جدید الزامی است.")
            .Equal(x => x.NewPassword).WithMessage(AuthErrorMessages.PasswordMismatch);

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage(AuthErrorMessages.NewPasswordSameAsCurrent)
            .When(x => !string.IsNullOrEmpty(x.CurrentPassword) && !string.IsNullOrEmpty(x.NewPassword));
    }
}
