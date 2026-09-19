namespace SmartX.Shared.Models;

public sealed record DeviceAlert
{
    public required Guid SensorId { get; init; }

    public required string DeviceIdentifier { get; init; }

    public required string State { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset FirstDetectedAtUtc { get; init; }
}