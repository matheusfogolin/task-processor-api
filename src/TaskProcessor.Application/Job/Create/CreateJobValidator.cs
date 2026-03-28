using FluentValidation;
using TaskProcessor.Domain.Shared.Errors;

namespace TaskProcessor.Application.Job.Create;

public class CreateJobValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage(JobErrors.TypeRequired.Description);

        RuleFor(x => x.Payload)
            .NotEmpty()
            .WithMessage(JobErrors.PayloadRequired.Description);
    }
}
