using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TaskProcessor.Application.Job.Create;
using TaskProcessor.Application.Shared;
using TaskProcessor.Application.Shared.Messages;
using TaskProcessor.Domain.Ports.MessageQueue;

namespace TaskProcessor.Tests.Application.Job.Create;

public class CreateJobHandlerTest
{
    private readonly Mock<IMessageQueuePublisher> _publisherMock;
    private readonly CreateJobHandler _handler;

    public CreateJobHandlerTest()
    {
        _publisherMock = new Mock<IMessageQueuePublisher>();
        var jobSettings = Options.Create(new JobSettings { MaxRetries = 3 });
        _handler = new CreateJobHandler(_publisherMock.Object, jobSettings);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPublishMessageAndReturnResponse()
    {
        var command = CreateValidCommand();
        var ct = CancellationToken.None;

        _publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<CreateJobMessage>(), ct))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, ct);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Type.Should().Be(command.Type);

        _publisherMock.Verify(
            x => x.PublishAsync(
                It.Is<CreateJobMessage>(m =>
                    m.Id == result.Value.Id
                    && m.Type == command.Type
                    && m.Payload == command.Payload
                    && m.MaxRetries == 3),
                ct),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldGenerateUniqueId()
    {
        var command = CreateValidCommand();
        var ct = CancellationToken.None;

        _publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<CreateJobMessage>(), ct))
            .Returns(Task.CompletedTask);

        var result1 = await _handler.Handle(command, ct);
        var result2 = await _handler.Handle(command, ct);

        result1.Value.Id.Should().NotBe(result2.Value.Id);
    }

    [Fact]
    public async Task Handle_WithNullMaxRetriesInSettings_ShouldUseDefaultOfThree()
    {
        var publisherMock = new Mock<IMessageQueuePublisher>();
        var jobSettings = Options.Create(new JobSettings { MaxRetries = null });
        var handler = new CreateJobHandler(publisherMock.Object, jobSettings);

        var command = CreateValidCommand();
        var ct = CancellationToken.None;

        publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<CreateJobMessage>(), ct))
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(command, ct);

        result.IsError.Should().BeFalse();

        publisherMock.Verify(
            x => x.PublishAsync(
                It.Is<CreateJobMessage>(m => m.MaxRetries == 3),
                ct),
            Times.Once);
    }

    private static CreateJobCommand CreateValidCommand() =>
        new("EnviarEmail", "{\"to\": \"user@example.com\", \"subject\": \"Teste\"}");
}
