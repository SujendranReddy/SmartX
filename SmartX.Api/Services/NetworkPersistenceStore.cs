
using System.Text.Json;
using SmartX.Api.Collections;
using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class NetworkPersistenceDocument
{
    public SensorProfile[] Sensors { get; set; } = [];
    public MeshConnection[] Connections { get; set; } = [];
}

public sealed class NetworkPersistenceStore
{
    private readonly string filePath;
    private readonly object syncRoot = new();

    private readonly JsonSerializerOptions options = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private NetworkPersistenceDocument state;

    public NetworkPersistenceStore(string filePath)
    {
        this.filePath = Path.GetFullPath(filePath);

        if (!File.Exists(this.filePath))
        {
            state = new NetworkPersistenceDocument();
            return;
        }

        var json = File.ReadAllText(this.filePath);

        state = JsonSerializer.Deserialize<NetworkPersistenceDocument>(
            json,
            options) ?? throw new InvalidDataException(
                "The saved network data is invalid.");

        if (state.Sensors is null || state.Connections is null)
        {
            throw new InvalidDataException(
                "The saved network data is incomplete.");
        }
    }

    public SensorProfile[] GetSensors()
    {
        lock (syncRoot)
        {
            return state.Sensors.ToArray();
        }
    }

    public MeshConnection[] GetConnections()
    {
        lock (syncRoot)
        {
            return state.Connections.ToArray();
        }
    }

    public bool IsPersistentNode(string nodeId)
    {
        lock (syncRoot)
        {
            return IsPersistentNodeInternal(nodeId);
        }
    }

    public void SaveSensor(SensorProfile sensor)
    {
        lock (syncRoot)
        {
            if (state.Sensors.Any(existing =>
                existing.Id == sensor.Id ||
                string.Equals(
                    existing.DeviceIdentifier,
                    sensor.DeviceIdentifier,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "This sensor is already saved.");
            }

            var updated = new NetworkPersistenceDocument
            {
                Sensors = [.. state.Sensors, sensor],
                Connections = state.Connections
            };

            SaveDocument(updated);
        }
    }

    public bool SaveConnection(MeshConnection connection)
    {
        lock (syncRoot)
        {
            if (!IsPersistentNodeInternal(connection.Source) ||
                !IsPersistentNodeInternal(connection.Target))
            {
                return false;
            }

            var connections = state.Connections
                .Where(existing =>
                    !MatchesConnection(existing, connection))
                .ToList();

            connections.Add(connection);

            var updated = new NetworkPersistenceDocument
            {
                Sensors = state.Sensors,
                Connections = connections.ToArray()
            };

            SaveDocument(updated);

            return true;
        }
    }

    private bool IsPersistentNodeInternal(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        if (string.Equals(
            nodeId.Trim(),
            MeshTopologyService.GatewayId,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return state.Sensors.Any(sensor =>
            string.Equals(
                sensor.DeviceIdentifier,
                nodeId.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesConnection(
        MeshConnection first,
        MeshConnection second)
    {
        return SameNode(first.Source, second.Source) &&
               SameNode(first.Target, second.Target) ||
               SameNode(first.Source, second.Target) &&
               SameNode(first.Target, second.Source);
    }

    private static bool SameNode(string first, string second)
    {
        return string.Equals(
            first,
            second,
            StringComparison.OrdinalIgnoreCase);
    }

    private void SaveDocument(NetworkPersistenceDocument updated)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                "The storage path is invalid.");

        Directory.CreateDirectory(directory);

        var temporaryFile = Path.Combine(
            directory,
            $".network-{Guid.NewGuid():N}.tmp");

        try
        {
            var json = JsonSerializer.Serialize(updated, options);

            File.WriteAllText(temporaryFile, json);
            File.Move(temporaryFile, filePath, true);

            state = updated;
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}
