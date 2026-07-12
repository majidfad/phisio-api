using FluentValidation.TestHelper;
using Phisio.Application.Auth;
using Phisio.Application.Auth.Validators;

namespace Phisio.Tests.Application.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenRequestIsInvalid_ShouldHaveValidationErrors()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = string.Empty,
            PhoneNumber = "invalid-phone",
            Password = string.Empty,
            ConfirmPassword = "DifferentPass1!"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void Validate_WhenPasswordsDoNotMatch_ShouldHaveConfirmPasswordError()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "DifferentPass1!"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage(AuthErrorMessages.PasswordMismatch);
    }
}
