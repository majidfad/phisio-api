namespace Phisio.Infrastructure.Identity;

internal static class AdminPasswordResolver
{
    public static (string Password, bool WasGenerated) Resolve(string? password, bool generatePassword)
    {
        if (generatePassword || string.IsNullOrWhiteSpace(password))
        {
            return (TemporaryPasswordGenerator.Generate(), true);
        }

        return (password, false);
    }
}
