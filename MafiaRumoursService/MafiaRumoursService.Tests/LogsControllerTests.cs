using Microsoft.Extensions.Logging;
using Moq;
using MafiaRumoursService.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace MafiaRumoursService.Tests;

public class LogsControllerTests : IDisposable
{
    private readonly string _testRunLogDirectory;
    private readonly string _logFile = "rumours-service.log";
    private readonly string _logFilePath;

    public LogsControllerTests()
    {
        _testRunLogDirectory = Path.Combine(Path.GetTempPath(), "LogsControllerTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testRunLogDirectory);
        _logFilePath = Path.Combine(_testRunLogDirectory, _logFile);

        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        File.WriteAllText(_logFilePath, "This is a test log entry.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRunLogDirectory))
            {
                 Directory.Delete(_testRunLogDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup error in test {GetType().Name}: {ex.Message}");
        }
    }

    private static void SetControllerLogFilePath(LogsController controller, string path)
    {
        var fieldInfo = typeof(LogsController).GetField("_logFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fieldInfo == null) throw new InvalidOperationException("Could not find the private field '_logFilePath' in LogsController.");
        fieldInfo.SetValue(controller, path);
    }


    [Fact]
    public void Constructor_Throws_WhenLoggerIsNull()
    {
        ILogger<LogsController> logger = null!;
        Assert.Throws<ArgumentNullException>(() => new LogsController(logger));
    }

    [Fact]
    public async Task DownloadLogs_ReturnsFile_WhenFileExists()
    {
        FileStreamResult? fileResult = null;
        try
        {
            var loggerMock = new Mock<ILogger<LogsController>>();
            var controller = new LogsController(loggerMock.Object);
            SetControllerLogFilePath(controller, _logFilePath);

            Assert.True(File.Exists(_logFilePath), $"Test setup failed: Log file should exist at {_logFilePath}");

            var result = controller.DownloadLogs();

            fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/octet-stream", fileResult.ContentType);
            Assert.Equal(_logFile, fileResult.FileDownloadName);
            Assert.True(fileResult.FileStream.Length > 0);
        }
        finally
        {
             if (fileResult?.FileStream != null)
             {
                 await fileResult.FileStream.DisposeAsync();
             }
             await Task.Delay(50);
        }
    }


    [Fact]
    public async Task DownloadLogs_ReturnsNotFound_WhenFileDoesNotExist()
    {
        var logger = new Mock<ILogger<LogsController>>();
        var controller = new LogsController(logger.Object);
        SetControllerLogFilePath(controller, _logFilePath);

        Assert.True(File.Exists(_logFilePath), "File must exist before deletion attempt.");

        File.Delete(_logFilePath);

        var retries = 5;
        while(File.Exists(_logFilePath) && retries > 0)
        {
            await Task.Delay(100);
            retries--;
        }
        Assert.False(File.Exists(_logFilePath), $"Test setup failed: Log file should NOT exist at {_logFilePath} after deletion.");


        var result = controller.DownloadLogs();

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        await Task.Delay(50);
    }
    
    [Fact]
    public async Task DownloadLogs_Returns500_WhenFileIsLocked()
    {
        if (!File.Exists(_logFilePath))
        {
            await File.WriteAllTextAsync(_logFilePath, "test log");
        }

        FileStream lockStream = null!;
        try
        {
            lockStream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            var loggerMock = new Mock<ILogger<LogsController>>();
            var controller = new LogsController(loggerMock.Object);
            SetControllerLogFilePath(controller, _logFilePath);

            var result = controller.DownloadLogs();

            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.NotNull(statusCodeResult.Value);
            Assert.Contains("An error occurred", statusCodeResult.Value.ToString());

            loggerMock.Verify(
                log => log.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred while trying to read the log file")),
                    It.IsAny<IOException>(), 
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            await lockStream.DisposeAsync();
        }
    }
}