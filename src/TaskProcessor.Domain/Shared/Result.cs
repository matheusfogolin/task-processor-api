namespace TaskProcessor.Domain.Shared;

public sealed class Result<TValue>
{
    private readonly TValue? _value;
    private readonly List<DomainError> _errors;

    private Result(TValue value)
    {
        _value = value;
        _errors = [];
    }

    private Result(List<DomainError> errors)
    {
        _value = default;
        _errors = errors;
    }

    public bool IsError => _errors.Count > 0;

    public TValue Value => !IsError
        ? _value!
        : throw new InvalidOperationException("Resultado contém erros.");

    public List<DomainError> Errors => IsError
        ? _errors
        : throw new InvalidOperationException("Resultado não contém erros.");

    public DomainError FirstError => Errors[0];

    public static implicit operator Result<TValue>(TValue value) => new(value);
    public static implicit operator Result<TValue>(DomainError error) => new([error]);
    public static implicit operator Result<TValue>(List<DomainError> errors) => new(errors);
}
