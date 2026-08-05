using Phisio.Application.Common;

namespace Phisio.Application.PatientSettings;

public interface IPatientSettingsService
{
    Task<AuthResult<PatientReminderSettingsDto>> GetReminderSettingsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientReminderSettingsDto>> UpdateReminderSettingsAsync(
        Guid patientId,
        UpdatePatientReminderSettingsRequest request,
        CancellationToken cancellationToken = default);
}
