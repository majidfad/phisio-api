using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Assignments;
using Phisio.Application.Common;

namespace Phisio.Tests.Api.Controllers;

public class AssignmentsControllerCreateAssignmentTests
{
    [Fact]
    public async Task CreateAssignment_WhenAssignmentSucceeds_ReturnsCreated()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            ExerciseId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901")
        };

        var assignment = new AssignmentDto(
            Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
            doctorId,
            request.PatientId,
            request.ExerciseId,
            "Hamstring Stretch",
            new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            true);

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.CreateAsync(doctorId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AssignmentDto>.Success(assignment));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.CreateAssignment(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(assignment);
    }

    [Fact]
    public async Task CreateAssignment_WhenAssignmentFails_ReturnsBadRequest()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            ExerciseId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901")
        };

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.CreateAsync(doctorId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AssignmentDto>.Failure(["Patient not found."]));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.CreateAssignment(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateAssignment_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            PatientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            ExerciseId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901")
        };

        var assignmentService = new Mock<IAssignmentService>();
        var controller = AssignmentsControllerTestHelper.CreateController(assignmentService);

        // Act
        var result = await controller.CreateAssignment(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}

public class AssignmentsControllerGetPatientAssignmentsTests
{
    [Fact]
    public async Task GetPatientAssignments_WhenAssignmentsExist_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var assignments = new List<AssignmentDto>
        {
            new(
                Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
                doctorId,
                patientId,
                Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                "Hamstring Stretch",
                new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                true)
        };

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.GetByPatientIdAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<AssignmentDto>>.Success(assignments));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.GetPatientAssignments(patientId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(assignments);
    }

    [Fact]
    public async Task GetPatientAssignments_WhenPatientNotLinked_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.GetByPatientIdAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<AssignmentDto>>.Failure(
                ["Patient not found or is not linked to this doctor via assignments."]));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.GetPatientAssignments(patientId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetPatientAssignments_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var assignmentService = new Mock<IAssignmentService>();
        var controller = AssignmentsControllerTestHelper.CreateController(assignmentService);

        // Act
        var result = await controller.GetPatientAssignments(
            Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}

public class AssignmentsControllerGetMyAssignmentsTests
{
    [Fact]
    public async Task GetMyAssignments_WhenAssignmentsExist_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var assignments = new List<AssignmentDto>
        {
            new(
                Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                patientId,
                Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                "Hamstring Stretch",
                new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                true)
        };

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.GetMyAssignmentsAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<AssignmentDto>>.Success(assignments));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(patientId));

        // Act
        var result = await controller.GetMyAssignments(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(assignments);
    }

    [Fact]
    public async Task GetMyAssignments_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var assignmentService = new Mock<IAssignmentService>();
        var controller = AssignmentsControllerTestHelper.CreateController(assignmentService);

        // Act
        var result = await controller.GetMyAssignments(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}

public class AssignmentsControllerDeactivateAssignmentTests
{
    [Fact]
    public async Task DeactivateAssignment_WhenDeactivationSucceeds_ReturnsNoContent()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var assignmentId = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012");

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.DeactivateAsync(doctorId, assignmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.DeactivateAssignment(assignmentId, CancellationToken.None);

        // Assert
        var noContentResult = result.Should().BeOfType<NoContentResult>().Subject;
        noContentResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DeactivateAssignment_WhenAssignmentNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var assignmentId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.DeactivateAsync(doctorId, assignmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure(["Assignment not found."]));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.DeactivateAssignment(assignmentId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<ObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeactivateAssignment_WhenAssignmentAlreadyInactive_ReturnsBadRequest()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var assignmentId = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012");

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.DeactivateAsync(doctorId, assignmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure(["Assignment is already inactive."]));

        var controller = AssignmentsControllerTestHelper.CreateController(
            assignmentService,
            AssignmentsControllerTestHelper.CreateAuthenticatedUser(doctorId));

        // Act
        var result = await controller.DeactivateAssignment(assignmentId, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<ObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeactivateAssignment_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var assignmentService = new Mock<IAssignmentService>();
        var controller = AssignmentsControllerTestHelper.CreateController(assignmentService);

        // Act
        var result = await controller.DeactivateAssignment(
            Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
            CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
