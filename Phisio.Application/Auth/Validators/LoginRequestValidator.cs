using FluentValidation;



namespace Phisio.Application.Auth.Validators;



public class LoginRequestValidator : AbstractValidator<LoginRequest>

{

    public LoginRequestValidator()

    {

        RuleFor(x => x.PhoneNumber)

            .NotEmpty()

            .MaximumLength(20)

            .Matches(@"^\+?[0-9\s\-()]+$")

            .WithMessage("Phone number format is invalid.");



        RuleFor(x => x.Password)

            .NotEmpty();

    }

}

