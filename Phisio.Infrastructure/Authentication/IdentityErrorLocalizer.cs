using Microsoft.AspNetCore.Identity;
using Phisio.Application.Auth;

namespace Phisio.Infrastructure.Authentication;

internal static class IdentityErrorLocalizer
{
    private static readonly Dictionary<string, string> MessagesByCode = new(StringComparer.Ordinal)
    {
        ["PasswordTooShort"] = "رمز عبور باید حداقل ۸ کاراکتر باشد.",
        ["PasswordRequiresNonAlphanumeric"] = "رمز عبور باید حداقل یک کاراکتر غیر حرفی داشته باشد.",
        ["PasswordRequiresDigit"] = "رمز عبور باید حداقل یک عدد داشته باشد.",
        ["PasswordRequiresLower"] = "رمز عبور باید حداقل یک حرف کوچک انگلیسی داشته باشد.",
        ["PasswordRequiresUpper"] = "رمز عبور باید حداقل یک حرف بزرگ انگلیسی داشته باشد.",
        ["DuplicateUserName"] = AuthErrorMessages.DuplicatePhoneNumber,
        ["DuplicateEmail"] = "این ایمیل قبلاً ثبت شده است.",
    };

    public static IEnumerable<string> Localize(IEnumerable<IdentityError> errors) =>
        errors.Select(error =>
            !string.IsNullOrEmpty(error.Code) && MessagesByCode.TryGetValue(error.Code, out var message)
                ? message
                : error.Description);
}
