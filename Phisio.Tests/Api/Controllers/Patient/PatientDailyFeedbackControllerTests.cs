using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.PatientDailyFeedback;

namespace Phisio.Tests.Api.Controllers.Patient;

public class PatientDailyFeedbackControllerTests
{
    [Fact]
    public async Task SubmitFeedback_WhenFeedbackSubmitted_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 4,
            HardnessScore = 3,
            Comment = "امروز بهتر بود.",
        };
        var response = new SubmitDailyFeedbackResponse(
            Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            patientId,
            doctorId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            4,
            3,
            "امروز بهتر بود.",
            false);

        var service = new Mock<IPatientDailyFeedbackService>();
        service.Setup(s => s.SubmitAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<SubmitDailyFeedbackResponse>.Success(response));

        var controller = CreateController(service, patientId);

        // Act
        var result = await controller.SubmitFeedback(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task SubmitFeedback_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var service = new Mock<IPatientDailyFeedbackService>();
        var controller = CreateController(service, userId: null);

        // Act
        var result = await controller.SubmitFeedback(
            new SubmitDailyFeedbackRequest { ImprovementScore = 4 },
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    private static PatientDailyFeedbackController CreateController(
        Mock<IPatientDailyFeedbackService> service,
        Guid? userId)
    {
        ClaimsPrincipal user = userId is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                authenticationType: "Test"));

        return new PatientDailyFeedbackController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }
}
