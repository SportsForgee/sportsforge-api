using System.Collections.Concurrent;
using System.Threading.Channels;
using ForgeInsole.Data.Entities;

namespace ForgeInsole.Api.Services
{
    // In-memory pub/sub keyed by InsoleId, so the SSE endpoint forwards the exact readings
    // the hosted generator persists rather than running a second, independent generator loop.
    public class TelemetryBroadcaster
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<TelemetryReading>>> _subscribers = new();

        public ChannelReader<TelemetryReading> Subscribe(string insoleId, out Guid subscriptionId)
        {
            var channel = Channel.CreateBounded<TelemetryReading>(new BoundedChannelOptions(16)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            subscriptionId = Guid.NewGuid();
            var subsForInsole = _subscribers.GetOrAdd(insoleId, _ => new ConcurrentDictionary<Guid, Channel<TelemetryReading>>());
            subsForInsole[subscriptionId] = channel;
            return channel.Reader;
        }

        public void Unsubscribe(string insoleId, Guid subscriptionId)
        {
            if (_subscribers.TryGetValue(insoleId, out var subsForInsole))
                subsForInsole.TryRemove(subscriptionId, out _);
        }

        public void Publish(TelemetryReading reading)
        {
            if (!_subscribers.TryGetValue(reading.InsoleId, out var subsForInsole)) return;
            foreach (var channel in subsForInsole.Values)
                channel.Writer.TryWrite(reading);
        }
    }
}
