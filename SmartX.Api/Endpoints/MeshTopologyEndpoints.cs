
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public sealed record MeshConnectionRequest(
    string Source,
    string Target,
    double Cost);

public static class MeshTopologyEndpoints
{
    public static void MapMeshTopologyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mesh");

        group.MapGet("/topology", (
            MeshTopologyService topology) =>
        {
            return Results.Ok(topology.GetSnapshot());
        });

        group.MapGet("/route", IResult (
            string sensorId,
            MeshTopologyService topology) =>
        {
            if (string.IsNullOrWhiteSpace(sensorId))
            {
                return Results.BadRequest(new
                {
                    message = "Provide a sensor identifier."
                });
            }

            try
            {
                var route = topology.GetSensorRoute(sensorId);
                return Results.Ok(route);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new
                {
                    message = "The requested sensor does not exist in the mesh network."
                });
            }
        });

        group.MapPost("/connections", IResult (
            MeshConnectionRequest request,
            MeshTopologyService topology) =>
        {
            if (string.IsNullOrWhiteSpace(request.Source) ||
                string.IsNullOrWhiteSpace(request.Target))
            {
                return Results.BadRequest(new
                {
                    message = "Both connection endpoints are required."
                });
            }

            if (!double.IsFinite(request.Cost) || request.Cost <= 0)
            {
                return Results.BadRequest(new
                {
                    message = "Connection cost must be a positive finite number."
                });
            }

            try
            {
                topology.AddConnection(
                    request.Source,
                    request.Target,
                    request.Cost);

                return Results.Ok(topology.GetSnapshot());
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new
                {
                    message = "One or both network nodes do not exist."
                });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new
                {
                    message = exception.Message
                });
            }
        });
    }
}
