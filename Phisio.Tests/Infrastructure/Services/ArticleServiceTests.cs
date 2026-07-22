using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Articles;
using Phisio.Application.Articles;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;

namespace Phisio.Tests.Infrastructure.Services;

public class ArticleServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsArticle()
    {
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ArticleService(dbContext.Object);

        var result = await sut.CreateAsync(new CreateArticleDto
        {
            Title = "Recovery tips",
            Summary = "Short summary",
            Body = "Full article body",
        });

        result.Succeeded.Should().BeTrue();
        result.Value!.Title.Should().Be("Recovery tips");
        dbContext.Object.Articles.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyMatchingEnabledStatus()
    {
        var dbContext = AppDbContextMockFactory.CreateMock();
        dbContext.Object.Articles.AddRange(
            new Article
            {
                ArticleId = Guid.NewGuid(),
                Title = "Active",
                Summary = "S",
                Body = "B",
                IsEnabled = true,
            },
            new Article
            {
                ArticleId = Guid.NewGuid(),
                Title = "Inactive",
                Summary = "S",
                Body = "B",
                IsEnabled = false,
            });
        await dbContext.Object.SaveChangesAsync();

        var sut = new ArticleService(dbContext.Object);

        var active = await sut.GetAllAsync(isEnabled: true);
        var inactive = await sut.GetAllAsync(isEnabled: false);

        active.Value.Should().ContainSingle().Which.Title.Should().Be("Active");
        inactive.Value.Should().ContainSingle().Which.Title.Should().Be("Inactive");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesArticle()
    {
        var articleId = Guid.NewGuid();
        var dbContext = AppDbContextMockFactory.CreateMock();
        dbContext.Object.Articles.Add(new Article
        {
            ArticleId = articleId,
            Title = "To delete",
            Summary = "S",
            Body = "B",
            IsEnabled = true,
        });
        await dbContext.Object.SaveChangesAsync();

        var sut = new ArticleService(dbContext.Object);

        var result = await sut.DeleteAsync(articleId);

        result.Succeeded.Should().BeTrue();
        dbContext.Object.Articles.Should().BeEmpty();
        dbContext.Object.Articles.IgnoreQueryFilters().Single().IsEnabled.Should().BeFalse();
    }
}
