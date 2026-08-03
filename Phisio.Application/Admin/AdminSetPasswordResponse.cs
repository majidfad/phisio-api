namespace Phisio.Application.Admin;

public sealed record AdminSetPasswordResponse(string Message, string? GeneratedPassword = null);
