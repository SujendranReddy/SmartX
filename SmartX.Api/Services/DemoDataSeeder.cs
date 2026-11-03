
using System.Diagnostics;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;

namespace SmartX.Api.Services;

public sealed class DemoDataSeeder
{
    private const int SensorCount = 1000;
    private const int DayCount = 7;

    private readonly SensorRegistry registry;
    private readonly TelemetryStore<float> floatStore;
    private readonly TelemetryStore<int> integerStore;
    private readonly TelemetryStore<bool> booleanStore;
    private readonly object syncRoot = new();

    private DemoSeedResult? completedResult;

    public DemoDataSeeder(
        SensorRegistry registry,
        TelemetryStore<float> floatStore,
        TelemetryStore<int> integerStore,
        TelemetryStore<bool> booleanStore)
    {
        this.registry = registry;
        this.floatStore = floatStore;
        this.integerStore = integerStore;
        this.booleanStore = booleanStore;
    }

    public DemoSeedResult Seed()
    {
        lock (syncRoot)
        {
            if (completedResult is not null)
            {
                return completedResult;
            }

            var stopwatch = Stopwatch.StartNew();
            var random = new Random(2026);

            var today = new DateTimeOffset(
                DateTime.UtcNow.Date,
                TimeSpan.Zero);

            var runIdentifier = Guid.NewGuid().ToString("N")[..8];
            var readingsCreated = 0;

            for (var sensorIndex = 0; sensorIndex < SensorCount; sensorIndex++)
            {
                var typeIndex = sensorIndex % 3;

                var category = typeIndex switch
                {
                    0 => SensorCategory.Environmental,
                    1 => SensorCategory.PowerConsumption,
                    _ => SensorCategory.Actuator
                };

                var dataType = typeIndex switch
                {
                    0 => TelemetryDataType.Float,
                    1 => TelemetryDataType.Integer,
                    _ => TelemetryDataType.Boolean
                };

                var prefix = typeIndex switch
                {
                    0 => "SOIL",
                    1 => "POWER",
                    _ => "VALVE"
                };

                var sensor = registry.Register(
                    new RegisterSensorRequest
                    {
                        DeviceIdentifier =
                            $"SIM-{runIdentifier}-{prefix}-{sensorIndex + 1:D4}",
                        DeploymentLocation =
                            $"Facility {sensorIndex / 100 + 1}, Greenhouse {sensorIndex % 10 + 1}",
                        Category = category,
                        DataType = dataType
                    },
                    persistent: false
                ) ?? throw new InvalidOperationException(
                    "A simulated device identifier is already registered.");

                readingsCreated += typeIndex switch
                {
                    0 => SeedSensor(
                        sensor.Id,
                        sensorIndex,
                        today,
                        floatStore,
                        () => (float)Math.Round(
                            30 + random.NextDouble() * 40,
                            2)),

                    1 => SeedSensor(
                        sensor.Id,
                        sensorIndex,
                        today,
                        integerStore,
                        () => random.Next(100, 1501)),

                    _ => SeedSensor(
                        sensor.Id,
                        sensorIndex,
                        today,
                        booleanStore,
                        () => random.Next(2) == 1)
                };
            }

            stopwatch.Stop();

            completedResult = new DemoSeedResult
            {
                SensorsCreated = SensorCount,
                ReadingsCreated = readingsCreated,
                DaysPerSensor = DayCount,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            return completedResult;
        }
    }

    private static int SeedSensor<T>(
        Guid sensorId,
        int sensorIndex,
        DateTimeOffset today,
        TelemetryStore<T> store,
        Func<T> createValue) where T : struct
    {
        var batches = new TelemetryPacket<T>[DayCount][];

        for (var dayIndex = 0; dayIndex < DayCount; dayIndex++)
        {
            var readingCount = 20 + (sensorIndex + dayIndex) % 11;

            var dayStart = today.AddDays(dayIndex - DayCount);

            batches[dayIndex] = new TelemetryPacket<T>[readingCount];

            for (var readingIndex = 0; readingIndex < readingCount; readingIndex++)
            {
                batches[dayIndex][readingIndex] = new TelemetryPacket<T>
                {
                    SensorId = sensorId,
                    Value = createValue(),
                    RecordedAtUtc = dayStart
                        .AddHours(8)
                        .AddMinutes(readingIndex * 15)
                };
            }
        }

        return store.AddBatch(sensorId, batches);
    }
}
