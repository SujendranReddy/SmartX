using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class TelemetryStore<T> where T : struct
{
    private readonly Dictionary<
        Guid,
        SortedDictionary<DateTimeOffset, List<TelemetryPacket<T>>>> readings = new();

    private readonly Dictionary<Guid, TelemetryPacket<T>> latestReadings = new();
    private readonly object syncRoot = new();

    public TelemetryPacket<T> Add(Guid sensorId, T value)
    {
        lock (syncRoot)
        {
            var packet = new TelemetryPacket<T>
            {
                SensorId = sensorId,
                Value = value,
                RecordedAtUtc = DateTimeOffset.UtcNow
            };

            StorePacket(packet);

            return packet;
        }
    }

    public int AddBatch(
        Guid sensorId,
        TelemetryPacket<T>[][] batches)
    {
        ArgumentNullException.ThrowIfNull(batches);

        var incoming = new List<TelemetryPacket<T>>();

        foreach (var batch in batches)
        {
            ArgumentNullException.ThrowIfNull(batch);

            foreach (var packet in batch)
            {
                if (packet is null)
                {
                    throw new ArgumentException(
                        "A telemetry batch cannot contain null packets.",
                        nameof(batches));
                }

                if (packet.SensorId != sensorId)
                {
                    throw new ArgumentException(
                        "Every packet must belong to the target sensor.",
                        nameof(batches));
                }

                if (packet.RecordedAtUtc == default)
                {
                    throw new ArgumentException(
                        "Every packet must have a recording timestamp.",
                        nameof(batches));
                }

                incoming.Add(packet);
            }
        }

        lock (syncRoot)
        {
            foreach (var packet in incoming)
            {
                StorePacket(packet);
            }
        }

        return incoming.Count;
    }

    public TelemetryPacket<T>? GetLatest(Guid sensorId)
    {
        lock (syncRoot)
        {
            return latestReadings.GetValueOrDefault(sensorId);
        }
    }

    public TelemetryPacket<T>[] GetHistory(Guid sensorId)
    {
        lock (syncRoot)
        {
            return readings.TryGetValue(sensorId, out var timeline)
                ? timeline.Values.SelectMany(packets => packets).ToArray()
                : [];
        }
    }

    public DailyTelemetryHistory<T> GetDailyHistory(Guid sensorId)
    {
        var days = GetHistory(sensorId)
            .GroupBy(packet =>
                DateOnly.FromDateTime(packet.RecordedAtUtc.UtcDateTime))
            .ToArray();

        return new DailyTelemetryHistory<T>
        {
            SensorId = sensorId,
            Dates = days.Select(day => day.Key).ToArray(),
            Readings = days.Select(day => day.ToArray()).ToArray()
        };
    }

    private void StorePacket(TelemetryPacket<T> packet)
    {
        if (!readings.TryGetValue(packet.SensorId, out var timeline))
        {
            timeline = new SortedDictionary<
                DateTimeOffset,
                List<TelemetryPacket<T>>>();

            readings.Add(packet.SensorId, timeline);
        }

        if (!timeline.TryGetValue(packet.RecordedAtUtc, out var packets))
        {
            packets = new List<TelemetryPacket<T>>();
            timeline.Add(packet.RecordedAtUtc, packets);
        }

        packets.Add(packet);

        if (!latestReadings.TryGetValue(packet.SensorId, out var latest) ||
            packet.RecordedAtUtc >= latest.RecordedAtUtc)
        {
            latestReadings[packet.SensorId] = packet;
        }
    }
}