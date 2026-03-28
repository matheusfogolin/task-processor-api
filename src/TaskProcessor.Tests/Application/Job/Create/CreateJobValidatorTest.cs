using FluentValidation.TestHelper;
using TaskProcessor.Application.Job.Create;
using TaskProcessor.Domain.Shared.Errors;

namespace TaskProcessor.Tests.Application.Job.Create;

public class CreateJobValidatorTest
{
    private readonly CreateJobValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        var command = new CreateJobCommand(
            "EnviarEmail",
            "{\"to\": \"user@example.com\"}");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyType_ShouldHaveValidationError()
    {
        var command = new CreateJobCommand("", "{\"to\": \"user@example.com\"}");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Type)
            .WithErrorMessage(JobErrors.TypeRequired.Description);
    }

    [Fact]
    public void Validate_WithEmptyPayload_ShouldHaveValidationError()
    {
        var command = new CreateJobCommand("EnviarEmail", "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Payload)
            .WithErrorMessage(JobErrors.PayloadRequired.Description);
    }
}
