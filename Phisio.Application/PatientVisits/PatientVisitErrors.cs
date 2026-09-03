namespace Phisio.Application.PatientVisits;

public static class PatientVisitErrors
{
    public const string PatientNotFound = "بیمار یافت نشد";
    public const string DoctorNotFound = "پزشک یافت نشد";
    public const string ClinicNotFound = "کلینیک یافت نشد";

    public const string ClinicManagerNotAuthorized = "دسترسی به کلینیک مجاز نیست";

    public const string DoctorMismatch = "شناسه پزشک نامعتبر است";
    public const string PatientNotConnectedToDoctor = "این بیمار به این پزشک متصل نیست";

    public const string VisitNotesMaxLengthExceeded = "یادداشت‌ها بیش از حد مجاز هستند";

    public const string VisitNotFound = "ویزیت یافت نشد";
    public const string FeedbackAlreadySubmitted = "بازخورد این ویزیت قبلاً ثبت شده است";
    public const string FeedbackScoresRequired = "امتیازهای بازخورد الزامی هستند";
}

