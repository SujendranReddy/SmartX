using System.ComponentModel.DataAnnotations;
using SmartX.Shared.Models;

namespace SmartX.Shared.Requests;

public sealed class SubmitTelemetryRequest<T> where T : struct
{
    [Required(ErrorMessage = "Provide a telemetry value.")]
    public T? Value { get; set; }

    public TelemetryPriority Priority { get; set; } = TelemetryPriority.Normal;
}