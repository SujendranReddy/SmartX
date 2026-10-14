# Smart-X

Smart-X is a simulated IoT gateway for registering sensors, submitting telemetry, monitoring devices and receiving suggested actions through a Blazor dashboard.

## Technology
- .NET 10 and ASP.NET Core Minimal APIs
- Blazor WebAssembly & Bootstrap styling
- No database or physical hardware, in-memory collections

### SmartX.Api
- API endpoints and validation
- Telemetry processing and storage
- Monitoring and alerts
- Device commands and activity tracking
- Recommendation engine

### SmartX.Client
- Browser dashboard and forms
- Telemetry history and graphs
- Device controls and Undo
- Suggested actions
- Attachments

### SmartX.Shared
- Models
- Requests
- Shared data

### SmartX.Tests
- xUnit automated tests
- Data structure and command testing

## Run Locally

### Requirements
- .NET 10 SDK
- Visual Studio with ASP.NET and web development workload (if using Visual Studio)

### How to Run (PowerShell)
1. Clone the repository.
2. Open PowerShell in the folder containing SmartX.slnx
3. Trust the development certificate, restore dependencies and build:
   - dotnet dev-certs https --trust
   - dotnet restore SmartX.slnx
   - dotnet build SmartX.slnx --no-restore
4. Start the API:
   - dotnet run --project SmartX.Api --launch-profile https
5. Leave that terminal running and open another terminal.
6. Start the Client:
   - dotnet run --project SmartX.Client --launch-profile https
7. Open https://localhost:7102 in your browser.

### How to Run Visual Studio
1. Install Visual Studio with the ASP.NET and web development workload and .NET 10 SDK.
2. Clone the repository and open SmartX.slnx.
3. Right-click the solution and select Configure Startup Projects.
4. Select Multiple startup projects:
   - SmartX.Api = Start
   - SmartX.Client = Start
   - SmartX.Shared = None
   - SmartX.Tests = None
5. Select Build = Build Solution.
6. Press Ctrl + F5 to run the application.
7. Open https://localhost:7102.

### Notes
- API health check: https://localhost:7101/api/health
- OpenAPI document: https://localhost:7101/openapi/v1.json
- Application data resets when the API restarts.
- Stop each application with Ctrl+C in its terminal.

## Features
- Register sensors and search or filter the sensor directory.
- Submit Float, Integer and Boolean telemetry.
- Process readings using FIFO and priority queues.
- Store and retrieve telemetry using dictionaries and sorted dictionaries.
- View daily telemetry history with timeline graphs.
- Monitor live sensor readings and detect warnings or stale devices
- Track unique active alerts using a HashSet.
- Control simulated actuators and Undo commands using a Stack.
- Record command and search activity.
- Generate recommendations based on live warnings and historical activity.
- Validate nested deployment structures.
- Upload and download protected sensor attachments.
- Generate demo data for testing.

## Testing
Automated tests are implemented using xUnit.

To run the tests:
- Open Test Explorer in Visual Studio and select Run All Tests.
- Or run dotnet test SmartX.Tests/SmartX.Tests.csproj.

The 13 tests cover priority queues, FIFO processing, Stack-based Undo, dictionaries, sorted telemetry history and alert management.

All 13 tests passed during final testing.

## Demo
1. Register an Environmental sensor with the Float data type.
2. Submit a reading of 10 and check Live Monitoring for a Warning
3. Open the Command Stream to view the suggested action.
4. Register a Boolean actuator, initialise it as OFF, turn it ON and use Undo.
5. Check command history to confirm the Undone status.
6. Submit multiple telemetry readings and view their timeline graph.
7. Record repeated searches or commands during warnings to demonstrate historical recommendations
8. Submit a normal reading of 50 and verify that the warning and recommendation disappear.
9. Upload and download a sensor attachment.
10. Load the demo dataset to test searching, pagination and historical readings.

## YouTube
Part 1:
https://youtu.be/nanmlA89OEk?si=8S9GPO4PL3lNkro1
