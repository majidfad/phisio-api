using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Articles;
using Phisio.Application.Articles;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class ArticleService : IArticleService
{
    private readonly AppDbContext _dbContext;

    public ArticleService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<ArticleDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var articles = await _dbContext.Articles
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .OrderByDescending(article => article.CreatedAt)
            .Select(article => MapToDto(article))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ArticleDto>>.Success(articles);
    }

    public async Task<AuthResult<ArticleDto>> GetByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await _dbContext.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ArticleId == articleId, cancellationToken);

        if (article is null)
        {
            return AuthResult<ArticleDto>.Failure(["Article not found."]);
        }

        return AuthResult<ArticleDto>.Success(MapToDto(article));
    }

    public async Task<AuthResult<ArticleDto>> CreateAsync(
        CreateArticleDto request,
        CancellationToken cancellationToken = default)
    {
        var article = new Article
        {
            ArticleId = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Summary = request.Summary.Trim(),
            Body = request.Body.Trim(),
        };

        _dbContext.Articles.Add(article);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ArticleDto>.Success(MapToDto(article));
    }

    public async Task<AuthResult<ArticleDto>> UpdateAsync(
        Guid articleId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var article = await _dbContext.Articles
            .FirstOrDefaultAsync(item => item.ArticleId == articleId, cancellationToken);

        if (article is null)
        {
            return AuthResult<ArticleDto>.Failure(["Article not found."]);
        }

        article.Title = request.Title.Trim();
        article.Summary = request.Summary.Trim();
        article.Body = request.Body.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ArticleDto>.Success(MapToDto(article));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await _dbContext.Articles
            .FirstOrDefaultAsync(item => item.ArticleId == articleId, cancellationToken);

        if (article is null)
        {
            return AuthResult<bool>.Failure(["Article not found."]);
        }

        article.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> ActivateAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await _dbContext.Articles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.ArticleId == articleId, cancellationToken);

        if (article is null)
        {
            return AuthResult<bool>.Failure(["Article not found."]);
        }

        article.IsEnabled = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private static ArticleDto MapToDto(Article article) =>
        new(
            article.ArticleId,
            article.Title,
            article.Summary,
            article.Body,
            article.CreatedAt,
            article.IsEnabled);
}
