using FluentValidation;

namespace Phisio.Application.Articles.Validators;

public sealed class UpdateArticleRequestValidator : AbstractValidator<UpdateArticleRequest>
{
    public UpdateArticleRequestValidator()
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
