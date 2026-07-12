using Phisio.Application.Auth;

using Phisio.Domain.Enums;

using Swashbuckle.AspNetCore.Filters;



namespace Phisio.Api.Swagger;



public class AuthResponseExample : IExamplesProvider<AuthResponse>

{

    public AuthResponse GetExamples() =>

        new(

            AccessToken: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.example.token",

            ExpiresAt: DateTime.UtcNow.AddHours(1),

            UserId: Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),

            PhoneNumber: "+15551234567",

            Email: "jane.smith@example.com",

            Name: "Dr. Jane Smith",

            Role: UserRole.Doctor);

}

