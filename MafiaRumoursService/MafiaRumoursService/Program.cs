using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MafiaRumoursService.Data;
using MafiaRumoursService.Middleware;
using MafiaRumoursService.Services;

Env.Load(options: LoadOptions.TraversePath());

var builder = WebApplication.CreateBuilder(args);

var maxConcurrentRequestsStr = Environment.GetEnvironmentVariable("MAX_CONCURRENT_REQUESTS") ?? "100";
if (!int.TryParse(maxConcurrentRequestsStr, out var maxConcurrentRequests))
{
    maxConcurrentRequests = 100;
}

builder.Services.AddSingleton(new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests));

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddHttpClient();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstErrorMessage = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage ?? "Invalid request.";
            var errorResponse = new { code = "VALIDATION_ERROR", message = firstErrorMessage };
            return new BadRequestObjectResult(errorResponse);
        };
    });

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
builder.Services.AddDbContext<RumoursDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IRumourService, PostgresRumourService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<RumoursDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.UseCors("CorsPolicy");
app.UseRouting();

app.UseMiddleware<RequestThrottlingMiddleware>();

app.MapControllers();

app.Run();