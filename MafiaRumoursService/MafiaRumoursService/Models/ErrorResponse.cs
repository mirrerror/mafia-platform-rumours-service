namespace MafiaRumoursService.Models;

public class ErrorResponse(string code, string message)
{
    public string Code { get; } = code;
    public string Message { get; } = message;
}