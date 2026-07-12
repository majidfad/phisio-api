using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Exercises;

namespace Phisio.Tests.Api.Controllers;

internal static class ExercisesControllerTestHelper
{
    public static ExercisesController CreateController(Mock<IExerciseService> exerciseService)
    {
        return new ExercisesController(exerciseService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
