using Phisio.Application.Admin.Articles;
using Phisio.Application.Common;

namespace Phisio.Application.Articles;

public interface IArticleService
{
    Task<AuthResult<IReadOnlyList<ArticleDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ArticleDto>> GetByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ArticleDto>> CreateAsync(
        CreateArticleDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ArticleDto>> UpdateAsync(
        Guid articleId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> ActivateAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);
}
