using Microsoft.AspNetCore.Mvc;

namespace MafiaRumoursService.Controllers;

[ApiController]
[Route("[controller]")]
public class LogsController(ILogger<LogsController> logger) : ControllerBase
{
    private readonly ILogger<LogsController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _logFilePath = Path.Combine("logs", "rumours-service.log");

    [HttpGet("download")]
    public IActionResult DownloadLogs()
    {
        _logger.LogInformation("Attempting to download log file from {LogFilePath}", _logFilePath);

        if (!System.IO.File.Exists(_logFilePath))
        {
            _logger.LogWarning("Log file not found at {LogFilePath}", _logFilePath);
            return NotFound($"Log file not found at {_logFilePath}");
        }

        try
        {
            var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            _logger.LogInformation("Successfully opened log file for download.");
            return File(stream, "application/octet-stream", Path.GetFileName(_logFilePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while trying to read the log file at {LogFilePath}", _logFilePath);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while reading the log file: {ex.Message}");
        }
    }
}