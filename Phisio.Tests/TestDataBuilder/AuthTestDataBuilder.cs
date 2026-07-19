using Phisio.Application.Auth;
using Phisio.Domain.Enums;

namespace Phisio.Tests.TestDataBuilder;

internal static class RegisterRequestBuilder
{
    public static RegisterRequest Valid() =>
        new()
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

    public static RegisterRequest ValidDoctor() =>
        new()
        {
            Name = "دکتر مریم احمدی",
            PhoneNumber = "09121112233",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = UserRole.Doctor,
            MedicalLicenseNumber = "123456",
            Specialty = "فیزیوتراپی"
        };
}

internal static class RegisterPatientRequestBuilder
{
    public static RegisterPatientRequest Valid() =>
        new()
        {
            Name = "Alice Patient",
            PhoneNumber = "+15559876543",
            Password = "SecurePass1!"
        };
}
