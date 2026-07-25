using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Admin.ExerciseCategories;
using Phisio.Application.Common;
using Phisio.Application.ExerciseCategories;

namespace Phisio.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/exercise-categories")]
public class AdminExerciseCategoriesController : ControllerBase
{
    private readonly IExerciseCategoryService _categoryService;

    public AdminExerciseCategoriesController(IExerciseCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExerciseCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetAllAsync(isEnabled, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExerciseCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetByIdAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExerciseCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateExerciseCategoryDto request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetCategory), new { id = result.Value!.ExerciseCategoryId }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ExerciseCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateExerciseCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateCategory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.ActivateAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains("Category not found.")
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return NoContent();
    }
}
