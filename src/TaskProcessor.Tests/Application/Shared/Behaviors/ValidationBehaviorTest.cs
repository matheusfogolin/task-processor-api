using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using TaskProcessor.Application.Shared.Behaviors;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Tests.Application.Shared.Behaviors;

public class ValidationBehaviorTest
{
    public record TestCommand(string Name) : IRequest<Result<string>>;

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("teste");
        var ct = CancellationToken.None;
        var nextCalled = false;

        RequestHandlerDelegate<Result<string>> next = (CancellationToken _) =>
        {
            nextCalled = true;
            Result<string> result = "sucesso";
            return Task.FromResult(result);
        };

        // Act
        var result = await behavior.Handle(command, next, ct);

        // Assert
        nextCalled.Should().BeTrue();
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("sucesso");
    }

    [Fact]
    public async Task Handle_WithPassingValidation_ShouldCallNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<TestCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<TestCommand>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<TestCommand, string>([validatorMock.Object]);
        var command = new TestCommand("teste");
        var ct = CancellationToken.None;
        var nextCalled = false;

        RequestHandlerDelegate<Result<string>> next = (CancellationToken _) =>
        {
            nextCalled = true;
            Result<string> result = "sucesso";
            return Task.FromResult(result);
        };

        // Act
        var result = await behavior.Handle(command, next, ct);

        // Assert
        nextCalled.Should().BeTrue();
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("sucesso");
    }

    [Fact]
    public async Task Handle_WithFailingValidation_ShouldReturnErrors()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Nome é obrigatório"),
            new("Email", "E-mail é obrigatório")
        };

        var validatorMock = new Mock<IValidator<TestCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<TestCommand>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var behavior = new ValidationBehavior<TestCommand, string>([validatorMock.Object]);
        var command = new TestCommand("");
        var ct = CancellationToken.None;
        var nextCalled = false;

        RequestHandlerDelegate<Result<string>> next = (CancellationToken _) =>
        {
            nextCalled = true;
            Result<string> result = "sucesso";
            return Task.FromResult(result);
        };

        // Act
        var result = await behavior.Handle(command, next, ct);

        // Assert
        nextCalled.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].Code.Should().Be("Name");
        result.Errors[0].Description.Should().Be("Nome é obrigatório");
        result.Errors[0].Type.Should().Be(EErrorType.Validation);
        result.Errors[1].Code.Should().Be("Email");
        result.Errors[1].Description.Should().Be("E-mail é obrigatório");
    }
}
