using Phisio.Application.Auth;
using Phisio.Domain.Enums;
using Swashbuckle.AspNetCore.Filters;

namespace Phisio.Api.Swagger;

public class RegisterResponseExample : IExamplesProvider<RegisterResponse>
{
    public RegisterResponse GetExamples() =>
        new(
            UserId: Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            PhoneNumber: "+989121234567",
            Name: "علی رضایی",
            Role: UserRole.Patient,
            Message: "Registration completed successfully. You can now login.");
}
