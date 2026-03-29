using System.Text;
using FluentAssertions;
using Moq;
using RabbitMQ.Client;
using TaskProcessor.Infrastructure.MessageQueue;

namespace TaskProcessor.Tests.Infrastructure;

public class RabbitMqConsumerGetRetryCountTest
{
    [Fact]
    public void GetRetryCount_HeadersNull_ReturnsZero()
    {
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns((IDictionary<string, object?>?)null);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(0);
    }

    [Fact]
    public void GetRetryCount_NoXDeathHeader_ReturnsZero()
    {
        var headers = new Dictionary<string, object?>();
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(0);
    }

    [Fact]
    public void GetRetryCount_SingleRejectedEntryWithCountOne_ReturnsOne()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = "jobs",
                ["reason"] = "rejected",
                ["count"] = 1L
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(1);
    }

    [Fact]
    public void GetRetryCount_RejectedEntryWithCountThree_ReturnsThree()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = "jobs",
                ["reason"] = "rejected",
                ["count"] = 3L
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(3);
    }

    [Fact]
    public void GetRetryCount_EntriesFromDifferentQueue_ReturnsZero()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = "other-queue",
                ["reason"] = "rejected",
                ["count"] = 5L
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(0);
    }

    [Fact]
    public void GetRetryCount_ByteArrayValues_ReturnsCorrectCount()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = Encoding.UTF8.GetBytes("jobs"),
                ["reason"] = Encoding.UTF8.GetBytes("rejected"),
                ["count"] = 2L
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(2);
    }

    [Fact]
    public void GetRetryCount_MixedEntries_CountsOnlyMatchingQueue()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = "jobs",
                ["reason"] = "rejected",
                ["count"] = 2L
            },
            new Dictionary<string, object>
            {
                ["queue"] = "other",
                ["reason"] = "rejected",
                ["count"] = 5L
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(2);
    }

    [Fact]
    public void GetRetryCount_EntryWithoutCountField_ReturnsOne()
    {
        var xDeathEntries = new List<object>
        {
            new Dictionary<string, object>
            {
                ["queue"] = "jobs",
                ["reason"] = "rejected"
            }
        };
        var headers = new Dictionary<string, object?> { ["x-death"] = xDeathEntries };
        var properties = new Mock<IReadOnlyBasicProperties>();
        properties.Setup(p => p.Headers).Returns(headers);

        var result = RabbitMqConsumer.GetRetryCount(properties.Object, "jobs");

        result.Should().Be(1);
    }
}
