
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;

namespace SmartX.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        MapTelemetryType<float>(app, "float", TelemetryDataType.Float);
        MapTelemetryType<int>(app, "integer", TelemetryDataType.Integer);
        MapTelemetryType<bool>(app, "boolean", TelemetryDataType.Boolean);
    }

    private static void MapTelemetryType<T>(
        WebApplication app,
        string route,
        TelemetryDataType expectedDataType) where T : struct
    {
        var group = app.MapGroup(
            $"/api/sensors/{{sensorId:guid}}/telemetry/{route}");

        group.MapPost("/", async Task<IResult> (
            Guid sensorId,
            SubmitTelemetryRequest<T> request,
            SensorRegistry registry,
            TelemetryQueue<T> queue) =>
        {
            var sensorError = ValidateSensor(
                registry,
                sensorId,
                expectedDataType);

            if (sensorError is not null)
            {
                return sensorError;
            }

            if (!request.Value.HasValue)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["Value"] = ["Provide a telemetry value."]
                    });
            }

            if (request.Value.Value is float floatValue &&
                !float.IsFinite(floatValue))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["Value"] = ["The reading must be a finite number."]
                    });
            }

            if (!Enum.IsDefined(request.Priority))
            {
                return Results.BadRequest("Choose a valid telemetry priority.");
            }

            var packet = await queue.EnqueueAsync(
                sensorId,
                request.Value.Value,
                request.Priority);

            return Results.Ok(packet);
        });

        group.MapGet("/", IResult (
            Guid sensorId,
            SensorRegistry registry,
            TelemetryStore<T> store) =>
        {
            var sensorError = ValidateSensor(
                registry,
                sensorId,
                expectedDataType);

            return sensorError ?? Results.Ok(store.GetHistory(sensorId));
        });

        group.MapGet("/daily", IResult (
            Guid sensorId,
            SensorRegistry registry,
            TelemetryStore<T> store) =>
        {
            var sensorError = ValidateSensor(
                registry,
                sensorId,
                expectedDataType);

            return sensorError ?? Results.Ok(store.GetDailyHistory(sensorId));
        });

        group.MapGet("/range", IResult (
            Guid sensorId,
            DateTimeOffset from,
            DateTimeOffset to,
            SensorRegistry registry,
            TelemetryStore<T> store) =>
        {
            var sensorError = ValidateSensor(
                registry,
                sensorId,
                expectedDataType);

            if (sensorError is not null)
            {
                return sensorError;
            }

            if (from > to)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["from"] = ["The start must not be after the end."]
                    });
            }

            return Results.Ok(store.GetRange(sensorId, from, to));
        });
    }

    private static IResult? ValidateSensor(
        SensorRegistry registry,
        Guid sensorId,
        TelemetryDataType expectedDataType)
    {
        var sensor = registry.GetById(sensorId);

        if (sensor is null)
        {
            return Results.Problem(
                title: "Sensor not found",
                detail: "Register the sensor before submitting or retrieving telemetry.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (sensor.DataType != expectedDataType)
        {
            return Results.Problem(
                title: "Telemetry data type mismatch",
                detail: $"This sensor accepts {sensor.DataType} readings.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }
}
