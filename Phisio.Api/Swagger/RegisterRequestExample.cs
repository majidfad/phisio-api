using Phisio.Application.Auth;
using Swashbuckle.AspNetCore.Filters;

namespace Phisio.Api.Swagger;

public class RegisterRequestExample : IExamplesProvider<RegisterRequest>
{
    public RegisterRequest GetExamples() =>
        new()
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };
}
