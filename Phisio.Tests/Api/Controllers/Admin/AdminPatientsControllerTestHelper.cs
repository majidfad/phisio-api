using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Admin.Patients;

namespace Phisio.Tests.Api.Controllers.Admin;

internal static class AdminPatientsControllerTestHelper
{
    public static AdminPatientsController CreateController(Mock<IAdminPatientService> adminPatientService)
    {
        return new AdminPatientsController(adminPatientService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
