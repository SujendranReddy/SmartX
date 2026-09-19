using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;

namespace SmartX.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/commands");

        group.MapGet("/", (CommandService commands) =>
        {
            return Results.Ok(commands.GetHistory());
        });

        group.MapPost("/{sensorId:guid}", IResult (
            Guid sensorId,
            IssueCommandRequest request,
            CommandService commands,
            ActivityService activities,
            MonitoringService monitoring) =>
        {
            if (!request.RequestedState.HasValue)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["RequestedState"] =
                            ["Choose whether the device should be on or off."]
                    });
            }

            try
            {
                var conditions = monitoring.GetSnapshot().Sensors
                    .Where(sensor => sensor.State is "Warning" or "Stale")
                    .ToArray();

                var command = commands.IssueCommand(
                    sensorId,
                    request.RequestedState.Value);

                activities.Record(new UserActivity
                {
                    SensorId = sensorId,
                    ActivityType = "Command",
                    RequestedState = request.RequestedState.Value,
                    RecordedAtUtc = command.IssuedAtUtc,
                    Conditions = conditions
                });

                return Results.Ok(command);
            }
            catch (KeyNotFoundException exception)
            {
                return Results.Problem(
                    title: "Device not found",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(
                    title: "Command could not be applied",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapPost("/undo", IResult (CommandService commands) =>
        {
            var command = commands.UndoLastCommand();

            if (command is null)
            {
                return Results.Problem(
                    title: "Nothing to undo",
                    detail: "Send a device command before trying to undo it.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Ok(command);
        });
    }
}