using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Patients;

namespace Phisio.Tests.Api.Controllers;

internal static class PatientsControllerTestHelper
{
    public static PatientsController CreateController(
        Mock<IPatientService> patientService,
        ClaimsPrincipal? user = null)
    {
        return new PatientsController(patientService.Object)
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

    public static ClaimsPrincipal CreateAuthenticatedDoctor(Guid doctorId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, doctorId.ToString())],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
