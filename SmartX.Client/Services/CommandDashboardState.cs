using SmartX.Shared.Models;

namespace SmartX.Client.Services;

public sealed class CommandDashboardState
{
    public Guid SelectedSensorId { get; set; } = Guid.Empty;

    public string SearchText { get; set; } = string.Empty;

    public SensorCategory? SelectedCategory { get; set; }

    public string SelectedState { get; set; } = string.Empty;
}