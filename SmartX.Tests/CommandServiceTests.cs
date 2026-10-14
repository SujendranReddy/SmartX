
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;
using Xunit;

namespace SmartX.Tests;

public sealed class CommandServiceTests
{
    [Fact]
    public void UndoRestoresPreviousState()
    {
        var (commands, store, sensorId) = CreateTestDevice();

        commands.IssueCommand(sensorId, true);

        Assert.True(store.GetLatest(sensorId)!.Value);

        var undone = commands.UndoLastCommand();

        Assert.NotNull(undone);
        Assert.False(store.GetLatest(sensorId)!.Value);
        Assert.NotNull(undone.UndoneAtUtc);
    }

    [Fact]
    public void UndoFollowsLastInFirstOutOrder()
    {
        var (commands, store, sensorId) = CreateTestDevice();

        commands.IssueCommand(sensorId, true);
        commands.IssueCommand(sensorId, false);

        var firstUndo = commands.UndoLastCommand();

        Assert.NotNull(firstUndo);
        Assert.False(firstUndo.RequestedState);
        Assert.True(store.GetLatest(sensorId)!.Value);

        var secondUndo = commands.UndoLastCommand();

        Assert.NotNull(secondUndo);
        Assert.True(secondUndo.RequestedState);
        Assert.False(store.GetLatest(sensorId)!.Value);
    }

    [Fact]
    public void UndoWithEmptyHistoryReturnsNull()
    {
        var (commands, _, _) = CreateTestDevice();

        var result = commands.UndoLastCommand();

        Assert.Null(result);
    }

    private static (
        CommandService Commands,
        TelemetryStore<bool> Store,
        Guid SensorId) CreateTestDevice()
    {
        var registry = new SensorRegistry();
        var store = new TelemetryStore<bool>();

        var sensor = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = "PUMP-TEST",
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Actuator,
            DataType = TelemetryDataType.Boolean
        })!;

        store.Add(sensor.Id, false);

        var commands = new CommandService(registry, store);

        return (commands, store, sensor.Id);
    }
}
