using FluentValidation;
using MediatR;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Application.Shared.Behaviors;

public class ValidationBehavior<TRequest, TResult>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, Result<TResult>>
    where TRequest : IRequest<Result<TResult>>
{
    public async Task<Result<TResult>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResult>> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var errors = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => DomainError.Validation(f.PropertyName, f.ErrorMessage))
            .ToList();

        if (errors.Count > 0)
            return errors;

        return await next(ct);
    }
}
