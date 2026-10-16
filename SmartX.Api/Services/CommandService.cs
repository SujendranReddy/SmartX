using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class CommandService
{
    private readonly SensorRegistry registry;
    private readonly TelemetryStore<bool> telemetryStore;
    private readonly Stack<DeviceCommand> undoStack = new();
    private readonly List<DeviceCommand> commandHistory = new();
    private readonly object syncRoot = new();

    public CommandService(
        SensorRegistry registry,
        TelemetryStore<bool> telemetryStore)
    {
        this.registry = registry;
        this.telemetryStore = telemetryStore;
    }

    public DeviceCommand IssueCommand(Guid sensorId, bool requestedState)
    {
        lock (syncRoot)
        {
            var sensor = registry.GetById(sensorId);

            if (sensor is null)
            {
                throw new KeyNotFoundException("The device was not found.");
            }

            if (sensor.Category != SensorCategory.Actuator ||
                sensor.DataType != TelemetryDataType.Boolean)
            {
                throw new InvalidOperationException(
                    "Commands can only control devices with an on/off state.");
            }

            var latestReading = telemetryStore.GetLatest(sensorId);

            if (latestReading is null)
            {
                throw new InvalidOperationException(
                    "Send an initial reading before controlling this device.");
            }

            if (latestReading.Value == requestedState)
            {
                throw new InvalidOperationException(
                    "The device is already in the requested state.");
            }

            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                SensorId = sensorId,
                PreviousState = latestReading.Value,
                RequestedState = requestedState,
                IssuedAtUtc = DateTimeOffset.UtcNow
            };

            telemetryStore.Add(sensorId, requestedState);
            undoStack.Push(command);
            commandHistory.Add(command);

            return CopyCommand(command);
        }
    }

    public DeviceCommand? UndoLastCommand()
    {
        lock (syncRoot)
        {
            if (!undoStack.TryPeek(out var command))
            {
                return null;
            }
            // Restore state before removing command
            telemetryStore.Add(command.SensorId, command.PreviousState);

            undoStack.Pop();
            command.UndoneAtUtc = DateTimeOffset.UtcNow;

            return CopyCommand(command);
        }
    }

    public DeviceCommand[] GetHistory()
    {
        lock (syncRoot)
        {
            return commandHistory
                .AsEnumerable()
                .Reverse()
                .Select(CopyCommand)
                .ToArray();
        }
    }

    private static DeviceCommand CopyCommand(DeviceCommand command)
    {
        return new DeviceCommand
        {
            Id = command.Id,
            SensorId = command.SensorId,
            PreviousState = command.PreviousState,
            RequestedState = command.RequestedState,
            IssuedAtUtc = command.IssuedAtUtc,
            UndoneAtUtc = command.UndoneAtUtc
        };
    }
}
