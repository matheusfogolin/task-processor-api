namespace TaskProcessor.Domain.Shared;

public sealed record DomainError(
    string Code,
    string Description,
    EErrorType Type = EErrorType.Failure)
{
    public static DomainError Validation(string code, string description) =>
        new(code, description, EErrorType.Validation);

    public static DomainError NotFound(string code, string description) =>
        new(code, description, EErrorType.NotFound);

    public static DomainError Conflict(string code, string description) =>
        new(code, description, EErrorType.Conflict);

    public static DomainError Failure(string code, string description) =>
        new(code, description, EErrorType.Failure);
}
