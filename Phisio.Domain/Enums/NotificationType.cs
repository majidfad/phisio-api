namespace Phisio.Domain.Enums;

public enum NotificationType
{
    PatientLinkRequested = 1,
    LinkApproved = 2,
    LinkRejected = 3,
    PatientRemoved = 4,
    ExercisesAssigned = 5,
    ProgramCreated = 6,
    ExercisesCompleted = 7,
    DailyFeedbackReceived = 8,
    DoctorPendingActivation = 9,
    DoctorActivated = 10,
    ExerciseReminder = 11,
}
