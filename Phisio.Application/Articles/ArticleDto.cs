namespace Phisio.Application.Articles;

public sealed record ArticleDto(
    Guid ArticleId,
    string Title,
    string Summary,
    string Body,
    DateTime CreatedAt,
    bool IsEnabled = true);
