using FluentValidation.TestHelper;
using Phisio.Application.Admin.Patients;
using Phisio.Application.Admin.Patients.Validators;

namespace Phisio.Tests.Application.Validators;

public class CreateAdminPatientDtoValidatorTests
{
    private readonly CreateAdminPatientDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111",
            Email = "alice@example.com"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenEmailIsOmitted_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111"
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
        var request = new CreateAdminPatientDto
        {
            Name = string.Empty,
            PhoneNumber = "invalid-phone",
            Email = "not-an-email"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class UpdateAdminPatientDtoValidatorTests
{
    private readonly UpdateAdminPatientDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new UpdateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111",
            Email = "alice@example.com"
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
        var request = new UpdateAdminPatientDto
        {
            Name = string.Empty,
            PhoneNumber = "invalid-phone",
            Email = "not-an-email"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
