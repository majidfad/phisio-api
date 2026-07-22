namespace Phisio.Application.Articles;

public sealed class UpdateArticleRequest
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
