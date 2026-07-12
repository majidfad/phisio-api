using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Admin.Assignments;
using Phisio.Application.Assignments;
using Phisio.Application.Common;

namespace Phisio.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/assignments")]
public class AdminAssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AdminAssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet("report")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReport(CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetReportAsync(cancellationToken);
        return Ok(result.Value);
    }
}
