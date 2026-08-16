namespace Phisio.Application.Common;

public static class PhoneNumberNormalizer
{
    public static string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? phoneNumber.Trim() : "+" + digits;
    }
}
