namespace Phisio.Infrastructure.Push;

public sealed class VapidSettings
{
    public const string SectionName = "Vapid";

    public string Subject { get; set; } = "mailto:support@zivan.app";

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
