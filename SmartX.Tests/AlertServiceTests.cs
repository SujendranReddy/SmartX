
using Microsoft.Extensions.Configuration;
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;
using Xunit;

namespace SmartX.Tests;

public sealed class AlertServiceTests
{
    [Fact]
    public void RepeatedChecksDoNotDuplicateWarnings()
    {
        var (alerts, store, sensorId) = CreateTestSensor();

        store.Add(sensorId, 10f);

        var first = Assert.Single(alerts.GetActiveAlerts());
        var second = Assert.Single(alerts.GetActiveAlerts());

        Assert.Equal("Warning", first.State);
        Assert.Equal(sensorId, second.SensorId);
        Assert.Equal(
            first.FirstDetectedAtUtc,
            second.FirstDetectedAtUtc);
    }

    [Fact]
    public void NormalReadingRemovesActiveWarning()
    {
        var (alerts, store, sensorId) = CreateTestSensor();

        store.Add(sensorId, 10f);

        Assert.Single(alerts.GetActiveAlerts());

        store.Add(sensorId, 50f);

        Assert.Empty(alerts.GetActiveAlerts());
    }

    [Fact]
    public void StaleReadingsGenerateUniqueAlerts()
    {
        var (alerts, store, sensorId) = CreateTestSensor();

        store.AddBatch(sensorId, new[]
        {
            new[]
            {
                new TelemetryPacket<float>
                {
                    SensorId = sensorId,
                    Value = 10f,
                    RecordedAtUtc =
                        DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            }
        });

        var first = Assert.Single(alerts.GetActiveAlerts());
        var second = Assert.Single(alerts.GetActiveAlerts());

        Assert.Equal("Stale", first.State);
        Assert.Equal(sensorId, first.SensorId);
        Assert.Equal(
            first.FirstDetectedAtUtc,
            second.FirstDetectedAtUtc);
    }

    [Fact]
    public void FreshWarningReplacesStaleAlert()
    {
        var (alerts, store, sensorId) = CreateTestSensor();

        store.AddBatch(sensorId, new[]
        {
            new[]
            {
                new TelemetryPacket<float>
                {
                    SensorId = sensorId,
                    Value = 10f,
                    RecordedAtUtc =
                        DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            }
        });

        Assert.Equal(
            "Stale",
            Assert.Single(alerts.GetActiveAlerts()).State);

        store.Add(sensorId, 10f);

        var current = Assert.Single(alerts.GetActiveAlerts());

        Assert.Equal("Warning", current.State);
        Assert.Equal(sensorId, current.SensorId);
    }

    private static (
        AlertService Alerts,
        TelemetryStore<float> Store,
        Guid SensorId) CreateTestSensor()
    {
        var registry = new SensorRegistry();
        var store = new TelemetryStore<float>();

        var sensor = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = "SOIL-TEST",
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Environmental,
            DataType = TelemetryDataType.Float
        })!;

        var monitoring = new MonitoringService(
            registry,
            store,
            new TelemetryStore<int>(),
            new TelemetryStore<bool>(),
            new ConfigurationManager());

        return (
            new AlertService(monitoring),
            store,
            sensor.Id);
    }
}
