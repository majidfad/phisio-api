namespace Phisio.Infrastructure.Persistence.Seeding;

/// <summary>
/// Initial admin credentials, read from the "SeedAdmin" configuration section.
/// Never commit real values to source control; provide them via deployment
/// configuration (environment variables, user-secrets, etc.).
///
/// Environment variables:
///   SeedAdmin__PhoneNumber=+10000000000
///   SeedAdmin__Password=Admin123!
///
/// Docker Compose:
///   environment:
///     SeedAdmin__PhoneNumber: "+10000000000"
///     SeedAdmin__Password: "Admin123!"
/// </summary>
public class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string? PhoneNumber { get; set; }

    public string? Password { get; set; }
}
