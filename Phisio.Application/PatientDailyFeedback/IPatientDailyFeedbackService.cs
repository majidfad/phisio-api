using Phisio.Application.Common;

namespace Phisio.Application.PatientDailyFeedback;

public interface IPatientDailyFeedbackService
{
    Task<AuthResult<SubmitDailyFeedbackResponse>> SubmitAsync(
        Guid patientId,
        SubmitDailyFeedbackRequest request,
        CancellationToken cancellationToken = default);
}
