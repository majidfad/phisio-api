using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Auth;

namespace Phisio.Tests.Api.Controllers;

internal static class AuthControllerTestHelper
{
    public static AuthController CreateController(
        Mock<IAuthService> authService,
        ClaimsPrincipal? user = null)
    {
        var controller = new AuthController(authService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return controller;
    }

    public static ClaimsPrincipal CreateAuthenticatedUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal CreateUserWithInvalidIdClaim(string invalidClaim = "not-a-guid")
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, invalidClaim)],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
