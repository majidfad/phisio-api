namespace Phisio.Infrastructure.Identity;

internal static class UserCredentials
{
    public static string NormalizePhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? phoneNumber.Trim() : "+" + digits;
    }

    public static IReadOnlyCollection<string> GetPhoneLookupValues(string phoneNumber)
    {
        var canonical = NormalizePhone(phoneNumber);
        if (string.IsNullOrEmpty(canonical))
        {
            return Array.Empty<string>();
        }

        var digitsOnly = canonical[1..];
        return digitsOnly.Length == 0
            ? [canonical]
            : [canonical, digitsOnly];
    }

    public static void Apply(ApplicationUser user, string phoneNumber, string? email)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        user.PhoneNumber = normalizedPhone;
        user.UserName = normalizedPhone;
        user.NormalizedUserName = normalizedPhone.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(email))
        {
            user.Email = null;
            user.NormalizedEmail = null;
            return;
        }

        user.Email = email.Trim();
        user.NormalizedEmail = user.Email.ToUpperInvariant();
    }
}
