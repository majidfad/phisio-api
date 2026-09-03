using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class Article : BaseEntity
{
    public Guid ArticleId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
