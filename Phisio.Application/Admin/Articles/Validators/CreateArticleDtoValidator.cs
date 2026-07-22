using FluentValidation;
using Phisio.Application.Admin.Articles;

namespace Phisio.Application.Admin.Articles.Validators;

public sealed class CreateArticleDtoValidator : AbstractValidator<CreateArticleDto>
{
    public CreateArticleDtoValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Summary)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(request => request.Body)
            .NotEmpty()
            .MaximumLength(20000);
    }
}
