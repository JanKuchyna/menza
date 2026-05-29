using System.Threading.Channels;

namespace UTB.Minute.WebApi;

public class OrderNotificationService
{
    private readonly List<Channel<string>> _channels = [];
    private readonly Lock _lock = new();

    public Channel<string> Subscribe()
    {
        var channel = Channel.CreateUnbounded<string>();
        lock (_lock) _channels.Add(channel);
        return channel;
    }

    public void Unsubscribe(Channel<string> channel)
    {
        lock (_lock) _channels.Remove(channel);
        channel.Writer.TryComplete();
    }

    public async Task NotifyAsync(string message)
    {
        List<Channel<string>> snapshot;
        lock (_lock) snapshot = [.._channels];
        foreach (var ch in snapshot)
            await ch.Writer.WriteAsync(message);
    }
}
