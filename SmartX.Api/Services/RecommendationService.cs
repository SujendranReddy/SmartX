
using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class RecommendationService
{
    private readonly ActivityService activities;
    private readonly MonitoringService monitoring;
    private readonly SensorRegistry registry;

    public RecommendationService(
        ActivityService activities,
        MonitoringService monitoring,
        SensorRegistry registry)
    {
        this.activities = activities;
        this.monitoring = monitoring;
        this.registry = registry;
    }

    public ActionRecommendation[] GetRecommendations()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = monitoring.GetSnapshot();
        var history = activities.GetHistory()
            .Where(activity =>
                activity.RecordedAtUtc >= now.AddDays(-30))
            .ToArray();

        var devices = registry.GetAll()
            .ToDictionary(device => device.Id);

        var statuses = snapshot.Sensors
            .ToDictionary(sensor => sensor.SensorId);

        var recommendations = new List<ActionRecommendation>();

        foreach (var sensor in snapshot.Sensors)
        {
            if (sensor.State is not ("Warning" or "Stale"))
            {
                continue;
            }

            var relatedActivities = history
                .Where(activity => activity.Conditions.Any(
                    condition =>
                        condition.SensorId == sensor.SensorId &&
                        condition.State == sensor.State))
                .ToArray();

            var commandPattern = relatedActivities
                .Where(activity =>
                    activity.ActivityType == "Command" &&
                    activity.SensorId.HasValue &&
                    activity.RequestedState.HasValue &&
                    devices.TryGetValue(
                        activity.SensorId.Value,
                        out var device) &&
                    device.Category == SensorCategory.Actuator &&
                    string.Equals(
                        device.DeploymentLocation,
                        sensor.DeploymentLocation,
                        StringComparison.OrdinalIgnoreCase))
                .GroupBy(activity => (
                    DeviceId: activity.SensorId!.Value,
                    State: activity.RequestedState!.Value))
                .OrderByDescending(group => group.Count())
                .FirstOrDefault();

            if (commandPattern is not null &&
                commandPattern.Count() >= 2 &&
                statuses.TryGetValue(
                    commandPattern.Key.DeviceId,
                    out var actuatorStatus) &&
                actuatorStatus.State == "Normal" &&
                actuatorStatus.LatestValue ==
                    (commandPattern.Key.State ? "False" : "True"))
            {
                var device = devices[commandPattern.Key.DeviceId];
                var requestedState = commandPattern.Key.State
                    ? "on"
                    : "off";

                recommendations.Add(new ActionRecommendation
                {
                    Title = $"Repeated response to {sensor.DeviceIdentifier}",
                    SuggestedAction =
                        $"Review {device.DeviceIdentifier} and consider turning it {requestedState}.",
                    Reason =
                        $"{sensor.DeviceIdentifier} is currently {sensor.State.ToLowerInvariant()}. " +
                        $"{commandPattern.Count()} previous commands changed " +
                        $"{device.DeviceIdentifier} to {requestedState} " +
                        "while the same sensor condition was present.",
                    Priority = "High",
                    SensorId = sensor.SensorId,
                    SupportingActivityCount = commandPattern.Count(),
                    GeneratedAtUtc = now
                });

                continue;
            }

            var searchPattern = relatedActivities
                .Where(activity =>
                    activity.ActivityType == "Search" &&
                    !string.IsNullOrWhiteSpace(activity.SearchText))
                .GroupBy(
                    activity => activity.SearchText!.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    SearchText = group.Key,
                    Count = CountSearchSessions(group)
                })
                .OrderByDescending(group => group.Count)
                .FirstOrDefault();

            if (searchPattern is not null &&
                searchPattern.Count >= 2)
            {
                recommendations.Add(new ActionRecommendation
                {
                    Title = $"Recurring issue with {sensor.DeviceIdentifier}",
                    SuggestedAction =
                        $"Inspect {sensor.DeviceIdentifier} and devices related to '{searchPattern.SearchText}'.",
                    Reason =
                        $"The sensor is currently {sensor.State.ToLowerInvariant()}. " +
                        $"Users previously searched for '{searchPattern.SearchText}' " +
                        $"in {searchPattern.Count} separate sessions " +
                        "while this sensor had the same condition.",
                    Priority = "High",
                    SensorId = sensor.SensorId,
                    SupportingActivityCount = searchPattern.Count,
                    GeneratedAtUtc = now
                });

                continue;
            }

            var suggestedAction = sensor.State == "Stale"
                ? $"Check the connection and telemetry updates for {sensor.DeviceIdentifier}."
                : $"Inspect {sensor.DeviceIdentifier} and investigate the abnormal reading.";

            recommendations.Add(new ActionRecommendation
            {
                Title = $"{sensor.State}: {sensor.DeviceIdentifier}",
                SuggestedAction = suggestedAction,
                Reason = sensor.Message,
                Priority = sensor.State == "Warning"
                    ? "High"
                    : "Normal",
                SensorId = sensor.SensorId,
                SupportingActivityCount = 0,
                GeneratedAtUtc = now
            });
        }

        return recommendations
            .OrderByDescending(recommendation =>
                recommendation.Priority == "High")
            .ThenByDescending(recommendation =>
                recommendation.SupportingActivityCount)
            .Take(20)
            .ToArray();
    }

    private static int CountSearchSessions(
        IEnumerable<UserActivity> searches)
    {
        var count = 0;
        DateTimeOffset? lastSearch = null;

        foreach (var search in searches.OrderBy(
            activity => activity.RecordedAtUtc))
        {
            if (!lastSearch.HasValue ||
                search.RecordedAtUtc - lastSearch.Value >=
                    TimeSpan.FromMinutes(5))
            {
                count++;
            }

            lastSearch = search.RecordedAtUtc;
        }

        return count;
    }
}
