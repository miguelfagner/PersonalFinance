namespace PersonalFinance.Api.Services;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
