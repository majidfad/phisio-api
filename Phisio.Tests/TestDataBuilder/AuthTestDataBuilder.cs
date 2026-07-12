using Phisio.Application.Auth;

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
