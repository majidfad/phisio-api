namespace Phisio.Application.DoctorPatients;

public static class DoctorPatientErrors
{
    public const string PatientNotFound = "بیمار یافت نشد";
    public const string DoctorNotFound = "پزشک یافت نشد";
    public const string RelationshipNotFound = "رابطه بیمار یافت نشد";
    public const string RequestNotFound = "درخواست یافت نشد";
    public const string AlreadyLinked = "این بیمار قبلاً به لیست شما اضافه شده است";
    public const string AlreadyRequested = "درخواست اتصال قبلاً ارسال شده است";
    public const string AlreadyApproved = "اتصال با این پزشک قبلاً تأیید شده است";
    public const string NotPending = "درخواست در وضعیت در انتظار نیست";
    public const string NotApproved = "اتصال تأییدشده‌ای یافت نشد";
    public const string NoExercisesSelected = "حداقل یک تمرین باید انتخاب شود";
    public const string NoDatesSelected = "حداقل یک تاریخ باید انتخاب شود";
    public const string NoValidExercises = "تمرین معتبری یافت نشد";
    public const string DuplicateAssignment = "برخی از تمرین‌ها برای تاریخ‌های انتخاب‌شده قبلاً ثبت شده‌اند";
    public const string ProgramNotFound = "برنامه تمرینی یافت نشد";
    public const string NoScheduleDates = "با قوانین انتخاب‌شده هیچ تاریخی تولید نشد";
}
