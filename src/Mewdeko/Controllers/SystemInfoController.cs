using System.Diagnostics;
using System.Threading;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller providing API endpoints for detailed system information.
///     Only accessible to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class SystemInfoController : ControllerBase
{
    private readonly BotCredentials credentials;
    private readonly ILogger<SystemInfoController> logger;
    private readonly PerformanceMonitorService performanceService;


    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemInfoController" /> class.
    /// </summary>
    /// <param name="credentials">The bot credentials for owner verification.</param>
    /// <param name="performanceService">The performance monitoring service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SystemInfoController(
        BotCredentials credentials,
        PerformanceMonitorService performanceService, ILogger<SystemInfoController> logger)
    {
        this.credentials = credentials;
        this.performanceService = performanceService;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets system information including CPU, memory, and uptime.
    /// </summary>
    /// <param name="userId">The Discord user ID to verify for owner permissions.</param>
    /// <returns>Current system resource utilization information.</returns>
    [HttpGet]
    public IActionResult GetSystemInfo([FromQuery] ulong userId)
    {
        // Check if the provided user ID is a bot owner
        if (!credentials.IsOwner(userId))
        {
            return Forbid();
        }

        try
        {
            // Get system info
            var currentProcess = Process.GetCurrentProcess();
            var cpuUsage = GetCpuUsage(currentProcess);
            var memoryInfo = GetMemoryInfo(currentProcess);
            var uptime = (DateTime.Now - currentProcess.StartTime);

            var topMethods = performanceService.GetTopCpuMethods(5);

            var result = new
            {
                cpuUsage,
                memoryUsageMb = memoryInfo.usedMemoryMb,
                memoryInfo.totalMemoryMb,
                uptime = uptime.ToString(@"dd\.hh\:mm\:ss"),
                processStartTime = currentProcess.StartTime,
                threadCount = currentProcess.Threads.Count,
                topMethods = topMethods.Select(m => new
                {
                    name = m.MethodName, avgTime = m.AvgExecutionTime
                }).ToArray()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting system information");
            return StatusCode(500, "Error retrieving system information");
        }
    }

    /// <summary>
    ///     Gets the CPU usage percentage for the current process.
    /// </summary>
    /// <param name="process">The process to measure.</param>
    /// <returns>CPU usage percentage.</returns>
    private double GetCpuUsage(Process process)
    {
        var startCpuUsage = process.TotalProcessorTime;
        var startTime = DateTime.UtcNow;

        // Small delay to measure CPU over time
        Thread.Sleep(500);

        var endCpuUsage = process.TotalProcessorTime;
        var endTime = DateTime.UtcNow;

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalElapsedMs = (endTime - startTime).TotalMilliseconds;

        var cpuUsagePercent = cpuUsedMs / (Environment.ProcessorCount * totalElapsedMs) * 100;

        return Math.Round(cpuUsagePercent, 2);
    }

    /// <summary>
    ///     Gets memory usage information for the current process.
    /// </summary>
    /// <param name="process">The process to measure.</param>
    /// <returns>A tuple containing used memory and total system memory in MB.</returns>
    private static (double usedMemoryMb, double totalMemoryMb) GetMemoryInfo(Process process)
    {
        var usedMemoryMb = process.WorkingSet64 / 1024.0 / 1024.0;

        double totalMemoryMb;
        if (OperatingSystem.IsWindows())
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            totalMemoryMb = gcMemoryInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0;
        }
        else if (OperatingSystem.IsLinux())
        {
            // Read memory info from /proc/meminfo on Linux
            try
            {
                var memInfo = System.IO.File.ReadAllText("/proc/meminfo");
                var memTotal = memInfo.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("MemTotal:"));

                if (memTotal != null)
                {
                    var parts = memTotal.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var totalKb))
                    {
                        totalMemoryMb = totalKb / 1024.0;
                    }
                    else
                    {
                        totalMemoryMb = 0;
                    }
                }
                else
                {
                    totalMemoryMb = 0;
                }
            }
            catch
            {
                totalMemoryMb = 0;
            }
        }
        else
        {
            totalMemoryMb = 0;
        }

        return (Math.Round(usedMemoryMb, 2), Math.Round(totalMemoryMb, 2));
    }
}