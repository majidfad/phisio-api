using FluentValidation.TestHelper;
using Phisio.Application.Auth;
using Phisio.Application.Auth.Validators;

namespace Phisio.Tests.Application.Validators;

public class RegisterPatientRequestValidatorTests
{
    private readonly RegisterPatientRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new RegisterPatientRequest
        {
            Name = "Alice Patient",
            PhoneNumber = "+15559876543",
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
        var request = new RegisterPatientRequest
        {
            Name = string.Empty,
            PhoneNumber = "invalid-phone",
            Password = "weak"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
