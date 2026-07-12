using FluentValidation;

namespace Phisio.Application.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام و نام خانوادگی الزامی است.")
            .MaximumLength(200).WithMessage("نام حداکثر ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("شماره موبایل الزامی است.")
            .MaximumLength(20).WithMessage("شماره موبایل حداکثر ۲۰ کاراکتر باشد.")
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("فرمت شماره موبایل نامعتبر است.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("رمز عبور الزامی است.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("تکرار رمز عبور الزامی است.")
            .Equal(x => x.Password).WithMessage(AuthErrorMessages.PasswordMismatch);
    }
}
