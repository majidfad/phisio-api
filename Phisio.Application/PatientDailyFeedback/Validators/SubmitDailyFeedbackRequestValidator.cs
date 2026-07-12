using FluentValidation;

namespace Phisio.Application.PatientDailyFeedback.Validators;

public class SubmitDailyFeedbackRequestValidator : AbstractValidator<SubmitDailyFeedbackRequest>
{
    public SubmitDailyFeedbackRequestValidator()
    {
        RuleFor(request => request.ImprovementScore)
            .InclusiveBetween(1, 5)
            .WithMessage(PatientDailyFeedbackErrors.InvalidImprovementScore);

        RuleFor(request => request.Comment)
            .MaximumLength(1000)
            .When(request => !string.IsNullOrWhiteSpace(request.Comment));
    }
}
