
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;
using Xunit;

namespace SmartX.Tests;

public sealed class DataStructureTests
{
    [Fact]
    public void DictionaryFindsRegisteredSensorById()
    {
        var registry = new SensorRegistry();

        var sensor = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = "SOIL-001",
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Environmental,
            DataType = TelemetryDataType.Float
        });

        Assert.NotNull(sensor);
        Assert.Same(sensor, registry.GetById(sensor.Id));
        Assert.Null(registry.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void RegistryPreventsDuplicateDeviceIdentifiers()
    {
        var registry = new SensorRegistry();

        var first = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = "PUMP-001",
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Actuator,
            DataType = TelemetryDataType.Boolean
        });

        var duplicate = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = "pump-001",
            DeploymentLocation = "Greenhouse B",
            Category = SensorCategory.Actuator,
            DataType = TelemetryDataType.Boolean
        });

        Assert.NotNull(first);
        Assert.Null(duplicate);
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void SortedDictionaryMaintainsChronologicalHistory()
    {
        var store = new TelemetryStore<int>();
        var sensorId = Guid.NewGuid();

        var first = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var second = first.AddMinutes(5);
        var third = first.AddMinutes(10);

        store.AddBatch(sensorId, new[]
        {
            new[]
            {
                CreatePacket(sensorId, 30, third),
                CreatePacket(sensorId, 10, first),
                CreatePacket(sensorId, 20, second)
            }
        });

        var history = store.GetHistory(sensorId);

        Assert.Equal(
            new[] { 10, 20, 30 },
            history.Select(packet => packet.Value));

        Assert.Equal(30, store.GetLatest(sensorId)!.Value);
    }

    [Fact]
    public void SortedDictionaryPreservesMatchingTimestamps()
    {
        var store = new TelemetryStore<int>();
        var sensorId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        store.AddBatch(sensorId, new[]
        {
            new[]
            {
                CreatePacket(sensorId, 15, timestamp),
                CreatePacket(sensorId, 25, timestamp)
            }
        });

        var history = store.GetHistory(sensorId);

        Assert.Equal(2, history.Length);
        Assert.Equal(
            new[] { 15, 25 },
            history.Select(packet => packet.Value));
    }

    private static TelemetryPacket<int> CreatePacket(
        Guid sensorId,
        int value,
        DateTimeOffset timestamp)
    {
        return new TelemetryPacket<int>
        {
            SensorId = sensorId,
            Value = value,
            RecordedAtUtc = timestamp
        };
    }
}
