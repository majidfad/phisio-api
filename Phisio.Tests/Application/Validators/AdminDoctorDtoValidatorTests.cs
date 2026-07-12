using FluentValidation.TestHelper;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Admin.Doctors.Validators;

namespace Phisio.Tests.Application.Validators;

public class CreateAdminDoctorDtoValidatorTests
{
    private readonly CreateAdminDoctorDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateAdminDoctorDto
        {
            Name = "Dr. Alice",
            PhoneNumber = "+15551111111",
            Email = "alice@example.com",
            Specialty = "Orthopedics",
            MedicalLicenseNumber = "MD-11111",
            ClinicAddress = "123 Clinic St"
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
        var request = new CreateAdminDoctorDto
        {
            Name = string.Empty,
            PhoneNumber = "invalid-phone",
            Email = "not-an-email",
            Specialty = string.Empty,
            MedicalLicenseNumber = string.Empty,
            ClinicAddress = string.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Specialty);
        result.ShouldHaveValidationErrorFor(x => x.MedicalLicenseNumber);
        result.ShouldHaveValidationErrorFor(x => x.ClinicAddress);
    }
}

public class UpdateAdminDoctorDtoValidatorTests
{
    private readonly UpdateAdminDoctorDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new UpdateAdminDoctorDto
        {
            Name = "Dr. Alice",
            PhoneNumber = "+15551111111",
            Specialty = "Orthopedics",
            MedicalLicenseNumber = "MD-11111",
            ClinicAddress = "123 Clinic St"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
