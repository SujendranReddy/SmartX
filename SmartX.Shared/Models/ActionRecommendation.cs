
namespace SmartX.Shared.Models;

public sealed record ActionRecommendation
{
    public required string Title { get; init; }

    public required string SuggestedAction { get; init; }

    public required string Reason { get; init; }

    public required string Priority { get; init; }

    public Guid? SensorId { get; init; }

    public int SupportingActivityCount { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }
}
