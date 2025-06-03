using Microsoft.AspNetCore.Mvc;
using cms.Data;
using Microsoft.EntityFrameworkCore;

namespace cms.Common.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HealthController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            // Check database connectivity
            await _context.Database.CanConnectAsync();

            return Ok(
                new
                {
                    status = "healthy", timestamp = DateTime.UtcNow, environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), database = "connected"
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(
                503,
                new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    database = "disconnected",
                    error = ex.Message
                }
            );
        }
    }

    /// <summary>
    /// Database health check
    /// </summary>
    [HttpGet("database")]
    public async Task<IActionResult> GetDatabaseHealth()
    {
        try
        {
            bool canConnect = await _context.Database.CanConnectAsync();
            int userCount = await _context.Users.CountAsync();

            return Ok(
                new
                {
                    status = "healthy", connected = canConnect, userCount = userCount, timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(
                503,
                new
                {
                    status = "unhealthy", connected = false, error = ex.Message, timestamp = DateTime.UtcNow
                }
            );
        }
    }
}
