using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");

        builder.HasKey(article => article.ArticleId);

        builder.Property(article => article.ArticleId)
            .ValueGeneratedNever();

        builder.Property(article => article.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(article => article.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(article => article.Body)
            .IsRequired()
            .HasMaxLength(20000);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(article => article.Title);
        builder.HasIndex(article => article.CreatedAt);
    }
}
