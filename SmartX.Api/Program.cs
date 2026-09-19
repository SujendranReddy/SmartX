using SmartX.Shared.Models;
using SmartX.Api.Endpoints;
using SmartX.Api.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// these share the in-memory stores across request for this API process
builder.Services.AddOpenApi();

builder.Services.AddDataProtection()
    .SetApplicationName("SmartX");

builder.Services.AddSingleton<SensorRegistry>();

builder.Services.AddSingleton<DeploymentValidator>();

// JSON nesting includes child arrays, so it allows more levels
// than the tree validators depth limit
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.MaxDepth = 256;
});

builder.Services.AddSingleton<SensorAttachmentStore>();

builder.Services.AddSingleton<TelemetryStore<float>>();

builder.Services.AddSingleton<TelemetryStore<int>>();

builder.Services.AddSingleton<TelemetryStore<bool>>();

builder.Services.AddSingleton<DemoDataSeeder>();

builder.Services.AddSingleton<MonitoringService>();

builder.Services.AddCors(options =>
{
    // the browse client runs on a separate HTTPS during local dev
    options.AddPolicy("Dashboard", policy =>
    {
        policy.WithOrigins("https://localhost:7102")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<CommandService>();

builder.Services.AddSingleton<TelemetryQueue<float>>();
builder.Services.AddSingleton<TelemetryQueue<int>>();
builder.Services.AddSingleton<TelemetryQueue<bool>>();

builder.Services.AddHostedService<TelemetryQueue<float>>(
    services => services.GetRequiredService<TelemetryQueue<float>>());

builder.Services.AddHostedService<TelemetryQueue<int>>(
    services => services.GetRequiredService<TelemetryQueue<int>>());

builder.Services.AddHostedService<TelemetryQueue<bool>>(

    services => services.GetRequiredService<TelemetryQueue<bool>>());

builder.Services.AddSingleton<AlertService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Dashboard");

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        application = "SmartX.Api"
    });
});

app.MapGet(
    "/api/power/aggregate",
    IResult (int firstWatts, int secondWatts, int limitWatts) =>
    {
        if (firstWatts < 0 || secondWatts < 0 || limitWatts <= 0)
        {
            return Results.Problem(
                title: "Invalid power values",
                detail: "Readings must be zero or greater and the limit must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var firstMeter = new PowerReading(firstWatts);
        var secondMeter = new PowerReading(secondWatts);
        var limit = new PowerReading(limitWatts);

        PowerReading total;

        try
        {
            total = firstMeter + secondMeter;
        }
        catch (OverflowException)
        {
            return Results.Problem(
                title: "Power total exceeds the supported range",
                detail: "The combined reading must not exceed 2,147,483,647 watts.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new
        {
            firstWatts = firstMeter.Watts,
            secondWatts = secondMeter.Watts,
            totalWatts = total.Watts,
            limitWatts = limit.Watts,
            isOverLimit = total > limit
        });
    });

app.MapSensorEndpoints(); 

app.MapTelemetryEndpoints();

app.MapSensorAttachmentEndpoints();

app.MapDeploymentEndpoints();

app.MapDemoEndpoints();

app.MapGet(
    "/api/monitoring",
    (MonitoringService monitoring) =>
    {
        return Results.Ok(monitoring.GetSnapshot());
    });
app.MapCommandEndpoints();

app.MapGet("/api/alerts", (AlertService alerts) =>
{
    return Results.Ok(alerts.GetActiveAlerts());
});

app.Run();