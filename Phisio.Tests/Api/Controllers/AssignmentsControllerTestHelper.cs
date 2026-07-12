using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Assignments;

namespace Phisio.Tests.Api.Controllers;

internal static class AssignmentsControllerTestHelper
{
    public static AssignmentsController CreateController(
        Mock<IAssignmentService> assignmentService,
        ClaimsPrincipal? user = null)
    {
        return new AssignmentsController(assignmentService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    public static ClaimsPrincipal CreateAuthenticatedUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
