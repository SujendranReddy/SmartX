namespace SmartX.Shared.Models;

public sealed record UserActivity
{
    public required Guid SensorId { get; init; }

    public required string ActivityType { get; init; }

    public bool? RequestedState { get; init; }

    public string? SearchText { get; init; }

    public required DateTimeOffset RecordedAtUtc { get; init; }

    public required SensorMonitorStatus[] Conditions { get; init; }
}