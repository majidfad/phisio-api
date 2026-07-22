namespace Phisio.Application.Admin.Articles;

public sealed class CreateArticleDto
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
