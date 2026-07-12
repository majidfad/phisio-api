using Phisio.Application.Auth;

using Swashbuckle.AspNetCore.Filters;



namespace Phisio.Api.Swagger;



public class LoginRequestExample : IExamplesProvider<LoginRequest>

{

    public LoginRequest GetExamples() =>

        new()

        {

            PhoneNumber = "+15551234567",

            Password = "SecurePass1!"

        };

}

