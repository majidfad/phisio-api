using FluentValidation.TestHelper;
using Phisio.Application.Clinics;
using Phisio.Application.Clinics.Validators;

namespace Phisio.Tests.Application.Validators;

public class CreateClinicDtoValidatorTests
{
    private readonly CreateClinicDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        var request = new CreateClinicDto
        {
            Name = "کلینیک مرکزی",
            Address = "تهران، خیابان ولیعصر",
            PhoneNumbers = ["02112345678", "+989121234567"],
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenRequiredFieldsMissing_ShouldHaveValidationErrors()
    {
        var request = new CreateClinicDto
        {
            Name = string.Empty,
            Address = string.Empty,
            PhoneNumbers = ["invalid-phone"],
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Address);
        result.ShouldHaveValidationErrorFor("PhoneNumbers[0]");
    }

    [Fact]
    public void Validate_WhenPhoneNumbersAreEmpty_ShouldHaveValidationError()
    {
        var request = new CreateClinicDto
        {
            Name = "مطب مرکزی",
            Address = "تهران",
            PhoneNumbers = [],
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PhoneNumbers);
    }
}

public class UpdateClinicDtoValidatorTests
{
    private readonly UpdateClinicDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        var request = new UpdateClinicDto
        {
            Name = "کلینیک بروزرسانی‌شده",
            Address = "آدرس جدید",
            PhoneNumbers = ["02187654321"],
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldHaveValidationError()
    {
        var request = new UpdateClinicDto
        {
            Name = string.Empty,
            Address = "Valid Address",
            PhoneNumbers = ["02187654321"],
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
