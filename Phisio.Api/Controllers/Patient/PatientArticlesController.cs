using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Articles;
using Phisio.Application.Common;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/articles")]
public class PatientArticlesController : ControllerBase
{
    private readonly IArticleService _articleService;

    public PatientArticlesController(IArticleService articleService)
    {
        _articleService = articleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ArticleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetArticles(CancellationToken cancellationToken = default)
    {
        var result = await _articleService.GetAllAsync(isEnabled: true, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(Guid id, CancellationToken cancellationToken)
    {
        var result = await _articleService.GetByIdAsync(id, cancellationToken);

        if (!result.Succeeded || result.Value is null || !result.Value.IsEnabled)
        {
            return NotFound(new { errors = result.Errors.Count > 0 ? result.Errors : ["Article not found."] });
        }

        return Ok(result.Value);
    }
}
