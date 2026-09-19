using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class TelemetryQueue<T> : BackgroundService where T : struct
{
    private readonly Queue<PendingReading> readings = new();

    private readonly PriorityQueue<PendingReading, (int Rank, long Order)>
        priorityReadings = new();

    private readonly SemaphoreSlim signal = new(0);
    private readonly object syncRoot = new();
    private readonly TelemetryStore<T> store;
    private long nextOrder;

    public TelemetryQueue(TelemetryStore<T> store)
    {
        this.store = store;
    }

    public Task<TelemetryPacket<T>> EnqueueAsync(
        Guid sensorId,
        T value,
        TelemetryPriority priority = TelemetryPriority.Normal)
    {
        var completion = new TaskCompletionSource<TelemetryPacket<T>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var reading = new PendingReading(sensorId, value, completion);

        lock (syncRoot)
        {
            if (priority == TelemetryPriority.Normal)
            {
                readings.Enqueue(reading);
            }
            else
            {
                var rank = priority == TelemetryPriority.Critical ? 0 : 1;

                priorityReadings.Enqueue(
                    reading,
                    (rank, nextOrder++));
            }
        }

        signal.Release();

        return completion.Task;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await signal.WaitAsync(stoppingToken);

                PendingReading reading;

                lock (syncRoot)
                {
                    reading = priorityReadings.Count > 0
                        ? priorityReadings.Dequeue()
                        : readings.Dequeue();
                }

                try
                {
                    var packet = store.Add(
                        reading.SensorId,
                        reading.Value);

                    reading.Completion.SetResult(packet);
                }
                catch (Exception exception)
                {
                    reading.Completion.SetException(exception);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (syncRoot)
            {
                while (priorityReadings.TryDequeue(out var urgent, out _))
                {
                    urgent.Completion.TrySetCanceled();
                }

                while (readings.TryDequeue(out var reading))
                {
                    reading.Completion.TrySetCanceled();
                }
            }
        }
    }

    private sealed record PendingReading(
        Guid SensorId,
        T Value,
        TaskCompletionSource<TelemetryPacket<T>> Completion);
}