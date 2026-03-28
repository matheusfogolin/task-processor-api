namespace TaskProcessor.Domain.Shared;

public readonly record struct Success;

public static class Result
{
    public static Success Ok => new();
}
