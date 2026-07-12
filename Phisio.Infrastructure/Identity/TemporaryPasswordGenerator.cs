namespace Phisio.Infrastructure.Identity;

internal static class TemporaryPasswordGenerator
{
    public static string Generate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"Phisio{suffix}!1";
    }
}
