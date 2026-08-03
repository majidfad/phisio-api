namespace Phisio.Application.Admin;

public sealed class AdminSetPasswordRequest
{
    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public bool GeneratePassword { get; set; }
}
