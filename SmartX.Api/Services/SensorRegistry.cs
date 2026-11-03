
using SmartX.Shared.Models;
using SmartX.Shared.Requests;

namespace SmartX.Api.Services;

public sealed class SensorRegistry
{
    private readonly Dictionary<Guid, SensorProfile> sensors = new();

    private readonly HashSet<string> deviceIdentifiers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object syncRoot = new();

    private readonly NetworkPersistenceStore? persistence;

    public SensorRegistry()
    {
    }

    public SensorRegistry(NetworkPersistenceStore persistence)
    {
        this.persistence = persistence;

        foreach (var sensor in persistence.GetSensors())
        {
            if (!sensors.TryAdd(sensor.Id, sensor) ||
                !deviceIdentifiers.Add(sensor.DeviceIdentifier))
            {
                throw new InvalidDataException(
                    "The saved sensor registry contains duplicate records.");
            }
        }
    }

    public SensorProfile[] GetAll()
    {
        lock (syncRoot)
        {
            return sensors.Values
                .OrderBy(sensor => sensor.DeviceIdentifier)
                .ToArray();
        }
    }

    public SensorProfile? GetById(Guid id)
    {
        lock (syncRoot)
        {
            return sensors.GetValueOrDefault(id);
        }
    }

    public SensorProfile? Register(
        RegisterSensorRequest request,
        bool persistent = true)
    {
        var deviceIdentifier = request.DeviceIdentifier.Trim();

        lock (syncRoot)
        {
            if (deviceIdentifiers.Contains(deviceIdentifier))
            {
                return null;
            }

            var sensor = new SensorProfile
            {
                Id = Guid.NewGuid(),
                DeviceIdentifier = deviceIdentifier,
                DeploymentLocation = request.DeploymentLocation.Trim(),
                Category = request.Category!.Value,
                DataType = request.DataType!.Value,
                RegisteredAtUtc = DateTimeOffset.UtcNow
            };

            if (persistent)
            {
                persistence?.SaveSensor(sensor);
            }

            sensors.Add(sensor.Id, sensor);
            deviceIdentifiers.Add(deviceIdentifier);

            return sensor;
        }
    }
}
