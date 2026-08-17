using System.Text;

namespace ClaimCheck;

/// <summary>Something travelling on the bus.</summary>
/// <param name="Subject">What it is about.</param>
/// <param name="Body">Its contents — either the payload itself, or a claim check.</param>
public readonly record struct BusMessage(string Subject, string Body)
{
    /// <summary>How much of the bus's allowance this message uses.</summary>
    public int SizeInBytes => Encoding.UTF8.GetByteCount(Body);
}

/// <summary>Thrown when a message exceeds the bus's limit.</summary>
public sealed class MessageTooLargeException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public MessageTooLargeException()
        : base("The message exceeds the bus's maximum size.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public MessageTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public MessageTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A message bus with a hard limit on message size.
///
/// **The limit is the point.** Every real broker has one — Service Bus 256KB on
/// its standard tier, Event Grid 1MB, Storage queues 64KB — and it is not
/// negotiable at run time. Without modelling it, the claim check would look like
/// indirection for its own sake, because in process nothing is ever too big for
/// anything.
/// </summary>
public sealed class MessageBus
{
    private readonly Queue<BusMessage> messages = new();

    /// <summary>Creates a bus accepting messages up to <paramref name="maxMessageBytes"/>.</summary>
    public MessageBus(int maxMessageBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessageBytes, 1);
        MaxMessageBytes = maxMessageBytes;
    }

    /// <summary>The largest message it will carry.</summary>
    public int MaxMessageBytes { get; }

    /// <summary>How many messages are waiting.</summary>
    public int Depth => messages.Count;

    /// <summary>Everything waiting, without consuming it.</summary>
    public IReadOnlyCollection<BusMessage> Peek() => messages;

    /// <summary>Puts a message on the bus.</summary>
    /// <exception cref="MessageTooLargeException">It is over the limit.</exception>
    public void Send(BusMessage message)
    {
        if (message.SizeInBytes > MaxMessageBytes)
        {
            throw new MessageTooLargeException(
                $"the message is {message.SizeInBytes} bytes and the bus accepts " +
                $"{MaxMessageBytes}");
        }

        messages.Enqueue(message);
    }

    /// <summary>Takes the next message, if there is one.</summary>
    public bool TryReceive(out BusMessage message)
    {
        if (messages.Count == 0)
        {
            message = default;
            return false;
        }

        message = messages.Dequeue();
        return true;
    }
}
