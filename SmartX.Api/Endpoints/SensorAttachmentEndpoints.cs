using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class SensorAttachmentEndpoints
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".txt",
            ".csv",
            ".log",
            ".jpg",
            ".jpeg",
            ".png",
            ".pdf"
        };

    private static readonly Dictionary<string, string[]> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".json"] = ["application/json", "text/json"],
            [".xml"] = ["application/xml", "text/xml"],
            [".yaml"] = ["application/yaml", "application/x-yaml", "text/yaml", "text/x-yaml"],
            [".yml"] = ["application/yaml", "application/x-yaml", "text/yaml", "text/x-yaml"],
            [".txt"] = ["text/plain"],
            [".csv"] = ["text/csv", "application/csv", "application/vnd.ms-excel"],
            [".log"] = ["text/plain", "application/octet-stream"],
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".pdf"] = ["application/pdf"]
        };

    public static void MapSensorAttachmentEndpoints(
        this WebApplication app)
    {
        var group = app.MapGroup(
            "/api/sensors/{sensorId:guid}/attachments");

        group.MapGet("/", IResult (
            Guid sensorId,
            SensorRegistry registry,
            SensorAttachmentStore store) =>
        {
            if (registry.GetById(sensorId) is null)
            {
                return SensorNotFound();
            }

            return Results.Ok(store.GetAll(sensorId));
        });

        group.MapPost("/", async Task<IResult> (
            Guid sensorId,
            IFormFile file,
            SensorRegistry registry,
            SensorAttachmentStore store,
            CancellationToken cancellationToken) =>
        {
            if (registry.GetById(sensorId) is null)
            {
                return SensorNotFound();
            }

            if (file.Length == 0)
            {
                return Results.Problem(
                    title: "Empty attachment",
                    detail: "Select a file that contains data.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (file.Length > MaxFileSize)
            {
                return Results.Problem(
                    title: "Attachment too large",
                    detail: "Each attachment must be 5 MiB or smaller.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }
            // remove any folder path from uploaded filename

            var fileName = Path.GetFileName(
                file.FileName.Replace('\\', '/'));

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Length > 150)
            {
                return Results.Problem(
                    title: "Invalid file name",
                    detail: "The file name must contain between 1 and 150 characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var extension = Path.GetExtension(fileName);

            if (!AllowedExtensions.Contains(extension))
            {
                return Results.Problem(
                    title: "Unsupported attachment type",
                    detail: "Use JSON, XML, YAML, YML, TXT, CSV, LOG, JPG, JPEG, PNG or PDF files.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var contentType = file.ContentType.Split(';', 2)[0].Trim();

            if (!AllowedContentTypes.TryGetValue(extension, out var allowedTypes) ||
                !allowedTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "Invalid attachment content type",
                    detail: "The uploaded file content type does not match its extension.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            using var buffer = new MemoryStream();

            await file.CopyToAsync(buffer, cancellationToken);

            var attachment = store.Add(
                sensorId,
                fileName,
                buffer.ToArray());
            // anti forgery check is disabled for uploads ...no cookie login
            
            return Results.Created(
                $"/api/sensors/{sensorId}/attachments/{attachment.Id}",
                attachment);
        })
        .DisableAntiforgery();

        group.MapGet("/{attachmentId:guid}", IResult (
            Guid sensorId,
            Guid attachmentId,
            SensorRegistry registry,
            SensorAttachmentStore store) =>
        {
            if (registry.GetById(sensorId) is null)
            {
                return SensorNotFound();
            }

            var attachment = store.GetById(sensorId, attachmentId);

            if (attachment is null)
            {
                return Results.Problem(
                    title: "Attachment not found",
                    detail: "No attachment matches this sensor and attachment ID.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.File(
                attachment.Content,
                contentType: "application/octet-stream",
                fileDownloadName: attachment.Metadata.FileName);
        });
    }

    private static IResult SensorNotFound()
    {
        return Results.Problem(
            title: "Sensor not found",
            detail: "Register the sensor before working with attachments.",
            statusCode: StatusCodes.Status404NotFound);
    }
}