using Phisio.Application.Auth;
using Swashbuckle.AspNetCore.Filters;

namespace Phisio.Api.Swagger;

public class RegisterPatientRequestExample : IExamplesProvider<RegisterPatientRequest>
{
    public RegisterPatientRequest GetExamples() =>
        new()
        {
            Name = "John Doe",
            PhoneNumber = "+989121234567",
            Password = "SecurePass1!"
        };
}
