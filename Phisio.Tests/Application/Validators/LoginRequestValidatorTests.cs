using FluentValidation.TestHelper;
using Phisio.Application.Auth;
using Phisio.Application.Auth.Validators;

namespace Phisio.Tests.Application.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new LoginRequest
        {
            PhoneNumber = "+15551234567",
            Password = "SecurePass1!"
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
        var request = new LoginRequest
        {
            PhoneNumber = string.Empty,
            Password = string.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
