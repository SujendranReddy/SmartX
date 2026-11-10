using System.ComponentModel.DataAnnotations;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;

namespace SmartX.Tests;

public sealed class Part1FeedbackTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", true)]
    [InlineData("aa:bb:cc:dd:ee:ff", true)]
    [InlineData("SOIL-001", false)]
    [InlineData("AA:BB:CC:DD:EE", false)]
    [InlineData("AA:BB:CC:DD:EE:GG", false)]
    [InlineData("AA-BB-CC-DD-EE-FF", false)]
    public void RegistrationValidatesMacAddress(string identifier, bool expectedValid)
    {
        var request = new RegisterSensorRequest
        {
            DeviceIdentifier = identifier,
            DeploymentLocation = "Test Lab",
            Category = SensorCategory.Environmental,
            DataType = TelemetryDataType.Float
        };

        var errors = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            errors,
            validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public void PowerReadingSubtractionReturnsNonNegativeDifference()
    {
        var result = new PowerReading(900) - new PowerReading(350);

        Assert.Equal(550, result.Watts);
    }

    [Fact]
    public void PowerReadingSubtractionRejectsNegativeResult()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new PowerReading(100) - new PowerReading(200);
        });
    }
}
