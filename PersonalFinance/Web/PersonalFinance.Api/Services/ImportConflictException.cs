namespace PersonalFinance.Api.Services;

public sealed class ImportConflictException(string message) : Exception(message);
