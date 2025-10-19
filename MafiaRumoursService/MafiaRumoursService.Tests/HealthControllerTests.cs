using MafiaRumoursService.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace MafiaRumoursService.Tests;

public class HealthControllerTests
{
    [Fact]
    public void GetHealth_ReturnsOkResult_WithUpStatus()
    {
        var controller = new HealthController();

        var result = controller.GetHealth();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        
        Assert.NotNull(value);
        var valueType = value.GetType();
        var statusProperty = valueType.GetProperty("status");
        var timestampProperty = valueType.GetProperty("timestamp");

        Assert.NotNull(statusProperty);
        Assert.NotNull(timestampProperty);
        
        Assert.Equal("UP", statusProperty.GetValue(value));
        Assert.True((DateTime)timestampProperty.GetValue(value)! <= DateTime.UtcNow);
    }
}