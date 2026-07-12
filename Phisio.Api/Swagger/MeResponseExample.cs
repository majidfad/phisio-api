using Phisio.Application.Auth;

using Swashbuckle.AspNetCore.Filters;



namespace Phisio.Api.Swagger;



public class MeResponseExample : IExamplesProvider<MeResponse>

{

    public MeResponse GetExamples() =>

        new(

            UserId: Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),

            PhoneNumber: "+15551234567",

            Email: "jane.smith@example.com",

            Roles: ["Doctor"]);

}

