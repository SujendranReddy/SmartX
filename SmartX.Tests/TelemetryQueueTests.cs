
using SmartX.Api.Services;
using SmartX.Shared.Models;
using Xunit;

namespace SmartX.Tests;

public sealed class TelemetryQueueTests
{
    [Fact]
    public async Task NormalReadingsFollowFifoOrder()
    {
        var result = await ProcessAsync(
            (10, TelemetryPriority.Normal),
            (20, TelemetryPriority.Normal),
            (30, TelemetryPriority.Normal));

        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public async Task CriticalReadingsBypassNormalReadings()
    {
        var result = await ProcessAsync(
            (10, TelemetryPriority.Normal),
            (20, TelemetryPriority.Normal),
            (30, TelemetryPriority.High),
            (40, TelemetryPriority.Critical),
            (50, TelemetryPriority.Critical),
            (60, TelemetryPriority.High));

        Assert.Equal(
            new[] { 40, 50, 30, 60, 10, 20 },
            result);
    }

    private static async Task<int[]> ProcessAsync(
        params (int Value, TelemetryPriority Priority)[] inputs)
    {
        var store = new TelemetryStore<int>();
        using var queue = new TelemetryQueue<int>(store);
        var sensorId = Guid.NewGuid();

        var pending = inputs
            .Select(input => queue.EnqueueAsync(
                sensorId,
                input.Value,
                input.Priority))
            .ToArray();

        await queue.StartAsync(CancellationToken.None);

        try
        {
            await Task.WhenAll(pending)
                .WaitAsync(TimeSpan.FromSeconds(5));

            return store.GetHistory(sensorId)
                .Select(packet => packet.Value)
                .ToArray();
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
