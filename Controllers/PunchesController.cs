using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PunchServerMVC.Data;
using PunchServerMVC.Models;

namespace PunchServerMVC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PunchesController : ControllerBase
    {
        private readonly IRepository _repo;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PunchesController> _logger;

        public PunchesController(IRepository repo, IWebHostEnvironment env, ILogger<PunchesController> logger)
        {
            _repo = repo;
            _env = env;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult SubmitPunch([FromBody] PunchRequest request)
        {
            _logger.LogInformation("Received PunchRequest: {@Request}", request);

            if (request == null)
                return BadRequest("Request body is missing");

            if (string.IsNullOrWhiteSpace(request.UniqueId))
                return BadRequest("UniqueId is required");

            if (string.IsNullOrWhiteSpace(request.ImageBase64))
                return BadRequest("ImageBase64 is required");

            var employee = _repo.GetEmployees().FirstOrDefault(e => e.UniqueId == request.UniqueId);
            if (employee == null)
            {
                _logger.LogWarning("Employee with UniqueId {UniqueId} not found", request.UniqueId);
                return BadRequest("Invalid UniqueId");
            }

            // Use consistent timestamp for both punch and image file
            var timestamp = request.PunchTime == DateTime.MinValue
                ? DateTime.UtcNow.ToLocalTime()
                : request.PunchTime.ToLocalTime();

            string fileName;
            try
            {
                var base64 = request.ImageBase64.Contains(',')
                    ? request.ImageBase64.Split(',').Last()
                    : request.ImageBase64;

                var bytes = Convert.FromBase64String(base64);

                fileName = $"{employee.Id}_{timestamp:yyyyMMdd_HHmmss}.jpg";
                var imageFolder = Path.Combine(_env.WebRootPath, "punch_images");
                Directory.CreateDirectory(imageFolder);
                var filePath = Path.Combine(imageFolder, fileName);

                System.IO.File.WriteAllBytes(filePath, bytes);
                _logger.LogInformation("Saved punch image to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save punch image.");
                return BadRequest("Invalid Base64 image data or file write error.");
            }

            // 🔍 Determine punch type from schedule
            var schedule = _repo.GetSchedules()
                .FirstOrDefault(s =>
                    s.EmployeeId == employee.Id &&
                    s.Days != null &&
                    s.Days.Contains(timestamp.DayOfWeek)
                );

            string punchType = "Unknown";
            if (schedule != null)
            {
                var margin = TimeSpan.FromMinutes(30);
                if (timestamp.TimeOfDay <= schedule.ShiftStart.Add(margin))
                    punchType = "In";
                else if (timestamp.TimeOfDay >= schedule.ShiftEnd.Subtract(margin))
                    punchType = "Out";
            }

            var punch = new Punch
            {
                EmployeeId = employee.Id,
                PunchType = punchType,
                Timestamp = timestamp,
                ImageUrl = fileName,
                CreatedAt = DateTime.UtcNow
            };

            _repo.AddPunch(punch);

            _logger.LogInformation("Punch saved for EmployeeId {EmployeeId} at {Timestamp} as {PunchType}",
                employee.Id, timestamp, punchType);

            return Ok(new { message = "Punch recorded successfully", timestamp, punchType });
        }
    }
}
