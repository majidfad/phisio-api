using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Exercises;

namespace Phisio.Tests.Api.Controllers.Admin;

internal static class AdminExercisesControllerTestHelper
{
    public static AdminExercisesController CreateController(
        Mock<IExerciseService> exerciseService,
        Mock<IExerciseVideoUploadService>? exerciseVideoUploadService = null)
    {
        exerciseVideoUploadService ??= new Mock<IExerciseVideoUploadService>();

        return new AdminExercisesController(
            exerciseService.Object,
            exerciseVideoUploadService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
    }
}
