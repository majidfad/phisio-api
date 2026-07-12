using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Assignments;

namespace Phisio.Tests.Api.Controllers.Admin;

internal static class AdminAssignmentsControllerTestHelper
{
    public static AdminAssignmentsController CreateController(
        Mock<IAssignmentService> assignmentService,
        ClaimsPrincipal? user = null)
    {
        return new AdminAssignmentsController(assignmentService.Object)
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

    public static ClaimsPrincipal CreateAuthenticatedAdmin(Guid adminId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, adminId.ToString())],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
