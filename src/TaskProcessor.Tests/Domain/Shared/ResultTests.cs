using FluentAssertions;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Tests.Domain.Shared;

public class ResultTests
{
    [Fact]
    public void Result_WithValue_ShouldNotBeError()
    {
        Result<int> result = 42;

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Result_WithError_ShouldBeError()
    {
        var error = DomainError.Validation("Test.Error", "Erro de teste");

        Result<int> result = error;

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Result_ImplicitConversionFromValue_ShouldSucceed()
    {
        Result<string> result = "valor";

        result.IsError.Should().BeFalse();
        result.Value.Should().Be("valor");
    }

    [Fact]
    public void Result_ImplicitConversionFromError_ShouldSucceed()
    {
        var error = DomainError.NotFound("Test.NotFound", "Não encontrado");

        Result<string> result = error;

        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.FirstError.Code.Should().Be("Test.NotFound");
        result.FirstError.Type.Should().Be(EErrorType.NotFound);
    }

    [Fact]
    public void Result_AccessValueWhenError_ShouldThrow()
    {
        Result<int> result = DomainError.Failure("Test.Fail", "Falhou");

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Result_AccessErrorsWhenSuccess_ShouldThrow()
    {
        Result<int> result = 10;

        var act = () => result.Errors;

        act.Should().Throw<InvalidOperationException>();
    }
}
