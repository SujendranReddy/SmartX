using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class AlertService
{
    private readonly MonitoringService monitoring;

    private readonly HashSet<(Guid SensorId, string State)> activeKeys = new();

    private readonly Dictionary<
        (Guid SensorId, string State),
        DeviceAlert> alerts = new();

    private readonly object syncRoot = new();

    public AlertService(MonitoringService monitoring)
    {
        this.monitoring = monitoring;
    }

    public DeviceAlert[] GetActiveAlerts()
    {
        lock (syncRoot)
        {
            var snapshot = monitoring.GetSnapshot();
            var currentKeys = new HashSet<(Guid SensorId, string State)>();

            foreach (var sensor in snapshot.Sensors)
            {
                if (sensor.State is not ("Warning" or "Stale"))
                {
                    continue;
                }

                var key = (sensor.SensorId, sensor.State);
                currentKeys.Add(key);

                if (activeKeys.Add(key))
                {
                    alerts.Add(key, new DeviceAlert
                    {
                        SensorId = sensor.SensorId,
                        DeviceIdentifier = sensor.DeviceIdentifier,
                        State = sensor.State,
                        Message = sensor.Message,
                        FirstDetectedAtUtc = snapshot.CheckedAtUtc
                    });
                }
                else
                {
                    alerts[key] = alerts[key] with
                    {
                        Message = sensor.Message
                    };
                }
            }

            var resolvedKeys = activeKeys
                .Except(currentKeys)
                .ToArray();

            foreach (var key in resolvedKeys)
            {
                activeKeys.Remove(key);
                alerts.Remove(key);
            }

            return alerts.Values
                .OrderByDescending(alert => alert.FirstDetectedAtUtc)
                .ToArray();
        }
    }
}