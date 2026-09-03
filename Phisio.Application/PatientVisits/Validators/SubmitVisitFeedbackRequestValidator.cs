using FluentValidation;
using Phisio.Domain.Entities;

namespace Phisio.Application.PatientVisits.Validators;

public class SubmitVisitFeedbackRequestValidator : AbstractValidator<SubmitVisitFeedbackRequest>
{
    public SubmitVisitFeedbackRequestValidator()
    {
        RuleFor(x => x.SatisfactionScore)
            .InclusiveBetween(PatientVisitFeedback.MinScore, PatientVisitFeedback.MaxScore);

        RuleFor(x => x.DoctorCommunicationScore)
            .InclusiveBetween(PatientVisitFeedback.MinScore, PatientVisitFeedback.MaxScore);

        RuleFor(x => x.Comment)
            .MaximumLength(PatientVisitFeedback.MaxCommentLength)
            .When(x => x.Comment is not null);
    }
}
