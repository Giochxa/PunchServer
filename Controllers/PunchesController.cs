<<<<<<< HEAD
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

            if (string.IsNullOrWhiteSpace(request.PersonalId))
                return BadRequest("PersonalId is required");

            var employee = _repo.GetEmployees().FirstOrDefault(e => e.PersonalId == request.PersonalId);
            if (employee == null)
            {
                _logger.LogWarning("Employee with PersonalId {PersonalId} not found", request.PersonalId);
                return BadRequest("Invalid PersonalId");
            }

            var timestamp = request.PunchTime == DateTime.MinValue
                ? DateTime.UtcNow.ToLocalTime()
                : request.PunchTime.ToLocalTime();

            string? fileName = null;
            if (!string.IsNullOrWhiteSpace(request.ImageBase64))
            {
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
                    _logger.LogError(ex, "Failed to decode or save punch image.");
                    // Do not fail sync if image fails; continue without it
                    fileName = null;
                }
            }

            // 🔍 Determine punch type
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

            _logger.LogInformation("✅ Punch saved for EmployeeId {EmployeeId} at {Timestamp} as {PunchType}",
                employee.Id, timestamp, punchType);

            return Ok(new
            {
                message = "Punch recorded successfully",
                timestamp,
                punchType
            });
        }
    }
}
=======
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
        private static readonly object SubmitPunchLock = new();
        private readonly IRepository _repo;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PunchesController> _logger;

        public PunchesController(IRepository repo, IWebHostEnvironment env, ILogger<PunchesController> logger)
        {
            _repo = repo;
            _env = env;
            _logger = logger;
        }

        private static bool IsScheduleMatchForTimestamp(Schedule s, DateTime ts, TimeSpan margin)
        {
            var effectiveDate = ts.Date;
            var effectiveDay = ts.DayOfWeek;

            bool isNightShift = s.ShiftEnd < s.ShiftStart;

            if (isNightShift)
            {
                // For night shifts, decide whether this punch belongs to:
                // - previous schedule day (morning / after-midnight side)
                // - current schedule day (evening / before-start side)
                //
                // Example: 18:00 -> 09:00
                // Gap between end and next start = 09:00 -> 18:00 = 9h
                // Midpoint = 13:30
                // Times before 13:30 belong to previous workday
                // Times after 13:30 belong to current workday

                var gap = s.ShiftStart - s.ShiftEnd; // e.g. 18:00 - 09:00 = 9h
                if (gap < TimeSpan.Zero)
                    gap = gap.Add(TimeSpan.FromDays(1));

                var cutoff = s.ShiftEnd + TimeSpan.FromTicks(gap.Ticks / 2);

                if (ts.TimeOfDay < cutoff)
                {
                    effectiveDate = ts.AddDays(-1).Date;
                    effectiveDay = ts.AddDays(-1).DayOfWeek;
                }
            }

            if (s.Days == null || s.Days.Count == 0)
                return true;

            if (!s.Days.Contains(effectiveDay))
                return false;

            if (s.StartDate.Date > effectiveDate) return false;
            if (s.EndDate.Date < effectiveDate) return false;

            return true;
        }

        private string? SavePunchImage(int employeeId, DateTime timestamp, string imageBase64)
        {
            try
            {
                var base64 = imageBase64.Contains(',')
                    ? imageBase64.Split(',').Last()
                    : imageBase64;

                var bytes = Convert.FromBase64String(base64);

                var fileName = $"{employeeId}_{timestamp.ToLocalTime():yyyyMMdd_HHmmss}.jpg";
                var imageFolder = Path.Combine(_env.WebRootPath, "punch_images");
                Directory.CreateDirectory(imageFolder);
                var filePath = Path.Combine(imageFolder, fileName);

                System.IO.File.WriteAllBytes(filePath, bytes);
                _logger.LogInformation("Saved punch image to: {FilePath}", filePath);

                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode or save punch image.");
                return null;
            }
        }

        private static DateTime NormalizeDuplicateTimestamp(DateTime timestamp)
        {
            var normalized = timestamp.Kind switch
            {
                DateTimeKind.Utc => timestamp.ToLocalTime(),
                DateTimeKind.Local => timestamp,
                _ => timestamp
            };

            return DateTime.SpecifyKind(normalized, DateTimeKind.Unspecified);
        }

        private static DateTime NormalizeDecisionTimestamp(DateTime timestamp)
        {
            return timestamp.Kind switch
            {
                DateTimeKind.Utc => timestamp.ToLocalTime(),
                DateTimeKind.Local => timestamp,
                _ => timestamp
            };
        }

        private Punch? FindDuplicatePunch(int employeeId, DateTime timestamp, TimeSpan duplicateWindow)
        {
            var normalizedIncoming = NormalizeDuplicateTimestamp(timestamp);

            return _repo.GetPunches()
                .Where(p => p.EmployeeId == employeeId)
                .Select(p => new
                {
                    Punch = p,
                    DifferenceSeconds = Math.Abs((NormalizeDuplicateTimestamp(p.Timestamp) - normalizedIncoming).TotalSeconds)
                })
                .Where(x => x.DifferenceSeconds <= duplicateWindow.TotalSeconds)
                .OrderBy(x => x.DifferenceSeconds)
                .ThenBy(x => x.Punch.Timestamp)
                .ThenByDescending(x => x.Punch.CreatedAt)
                .Select(x => x.Punch)
                .FirstOrDefault();
        }

        private IActionResult BuildDuplicateResponse(Punch duplicatePunch, int employeeId, DateTime timestamp, PunchRequest request, string logPrefix)
        {
            var existingHasImage = !string.IsNullOrWhiteSpace(duplicatePunch.ImageUrl);
            var incomingHasImage = !string.IsNullOrWhiteSpace(request.ImageBase64);

            if (!existingHasImage && incomingHasImage)
            {
                var fileName = SavePunchImage(employeeId, duplicatePunch.Timestamp, request.ImageBase64!);

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    duplicatePunch.ImageUrl = fileName;
                    duplicatePunch.UpdatedAt = DateTime.UtcNow;

                    _repo.UpdatePunch(duplicatePunch);

                    _logger.LogInformation(
                        "{LogPrefix} duplicate matched existing record and image was added for EmployeeId {EmployeeId} at {Timestamp}",
                        logPrefix,
                        employeeId,
                        duplicatePunch.Timestamp);

                    return Ok(new
                    {
                        message = "Duplicate punch matched existing record and image was added",
                        timestamp = duplicatePunch.Timestamp,
                        punchType = duplicatePunch.PunchType
                    });
                }
            }

            _logger.LogWarning(
                "{LogPrefix} duplicate ignored for EmployeeId {EmployeeId}. IncomingTimestamp={IncomingTimestamp}, ExistingTimestamp={ExistingTimestamp}, ExistingHasImage={ExistingHasImage}, IncomingHasImage={IncomingHasImage}",
                logPrefix,
                employeeId,
                timestamp,
                duplicatePunch.Timestamp,
                existingHasImage,
                incomingHasImage);

            return Ok(new
            {
                message = "Duplicate punch ignored",
                timestamp = duplicatePunch.Timestamp,
                punchType = duplicatePunch.PunchType
            });
        }

        [HttpPost]
        public IActionResult SubmitPunch([FromBody] PunchRequest request)
        {
            _logger.LogInformation("Received PunchRequest: {@Request}", request);

            if (request == null)
                return BadRequest("Request body is missing");

            if (string.IsNullOrWhiteSpace(request.PersonalId))
                return BadRequest("PersonalId is required");

            var employee = _repo.GetEmployees().FirstOrDefault(e => e.PersonalId == request.PersonalId);
            if (employee == null)
            {
                _logger.LogWarning("Employee with PersonalId {PersonalId} not found", request.PersonalId);
                return BadRequest("Invalid PersonalId");
            }

            var timestamp = request.PunchTime == DateTime.MinValue
                ? DateTime.UtcNow
                : request.PunchTime;
            var decisionTimestamp = NormalizeDecisionTimestamp(timestamp);

            lock (SubmitPunchLock)
            {
                var duplicateWindow = TimeSpan.FromSeconds(60);
                var duplicatePunch = FindDuplicatePunch(employee.Id, timestamp, duplicateWindow);

                _logger.LogWarning(
                    "Duplicate check: EmployeeId={EmployeeId}, IncomingTimestamp={IncomingTimestamp}, WindowSeconds={WindowSeconds}, Found={Found}, ExistingTimestamp={ExistingTimestamp}",
                    employee.Id,
                    timestamp,
                    duplicateWindow.TotalSeconds,
                    duplicatePunch != null,
                    duplicatePunch?.Timestamp);

                if (duplicatePunch != null)
                    return BuildDuplicateResponse(duplicatePunch, employee.Id, timestamp, request, "Window");

                // 2) Get last punch for business logic
                var lastPunch = _repo.GetPunches()
                    .Where(p => p.EmployeeId == employee.Id)
                    .OrderByDescending(p => p.Timestamp)
                    .ThenByDescending(p => p.CreatedAt)
                    .FirstOrDefault();

                _logger.LogInformation(
                    "Last punch check: EmployeeId={EmployeeId}, LastType={LastType}, LastTimestamp={LastTimestamp}",
                    employee.Id,
                    lastPunch?.PunchType,
                    lastPunch?.Timestamp);

                // 3) Base punch type (toggle) + missed OUT safety flag
                string punchType;
                bool forceInSafety = false;

                if (lastPunch == null)
                {
                    punchType = "In"; // First punch ever
                }
                else
                {
                    // Default toggle
                    punchType = lastPunch.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase)
                        ? "Out"
                        : "In";

                    _logger.LogInformation(
                        "After toggle: EmployeeId={EmployeeId}, InitialPunchType={PunchType}",
                        employee.Id,
                        punchType);

                    // Missed "Out" protection (>16 hours)
                    var gap = decisionTimestamp - NormalizeDecisionTimestamp(lastPunch.Timestamp);
                    if (lastPunch.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase) &&
                        gap.TotalHours > 16)
                    {
                        forceInSafety = true;
                        _logger.LogWarning(
                            "Missed OUT safety triggered: last punch was IN and gap {Hours:F1}h > 16h for Employee {EmployeeId}.",
                            gap.TotalHours, employee.Id);
                    }
                }

                var margin = TimeSpan.FromMinutes(15);

                var schedule = _repo.GetSchedules()
                    .Where(s => s.EmployeeId == employee.Id)
                    .Where(s => IsScheduleMatchForTimestamp(s, decisionTimestamp, margin))
                    .OrderByDescending(s => s.StartDate)
                    .ThenByDescending(s => s.EndDate)
                    .FirstOrDefault();

                _logger.LogInformation(
                    "Schedule match: EmployeeId={EmployeeId}, Matched={Matched}, Timestamp={Timestamp}, DecisionTimestamp={DecisionTimestamp}, Day={Day}, Time={Time}, ShiftStart={ShiftStart}, ShiftEnd={ShiftEnd}",
                    employee.Id,
                    schedule != null,
                    timestamp,
                    decisionTimestamp,
                    decisionTimestamp.DayOfWeek,
                    decisionTimestamp.TimeOfDay,
                    schedule?.ShiftStart,
                    schedule?.ShiftEnd);

                bool scheduleDeterminedPunch = false;

                if (schedule != null)
                {
                    bool isNightShift = schedule.ShiftEnd < schedule.ShiftStart;

                    _logger.LogInformation(
                        "Schedule detected: isNightShift={IsNightShift}",
                        isNightShift);

                    if (!isNightShift)
                    {
                        // Day shift:
                        // first half => In
                        // second half => Out
                        var shiftLength = schedule.ShiftEnd - schedule.ShiftStart;
                        var midpoint = schedule.ShiftStart.Add(TimeSpan.FromTicks(shiftLength.Ticks / 2));

                        _logger.LogInformation(
                            "Day shift: midpoint={Midpoint}, Time={Time}",
                            midpoint,
                            decisionTimestamp.TimeOfDay);

                        if (decisionTimestamp.TimeOfDay < midpoint)
                        {
                            punchType = "In";
                            scheduleDeterminedPunch = true;
                            _logger.LogInformation("Decision: DAY => IN");
                        }
                        else
                        {
                            punchType = "Out";
                            scheduleDeterminedPunch = true;
                            _logger.LogInformation("Decision: DAY => OUT");
                        }
                    }
                    else
                    {
                        // Night shift: use the SAME cutoff as IsScheduleMatchForTimestamp
                        // Example 18:00 -> 09:00
                        // gap from 09:00 to 18:00 = 9h
                        // cutoff = 13:30
                        // before cutoff  => Out
                        // from cutoff on => In
                        var gap = schedule.ShiftStart - schedule.ShiftEnd;
                        if (gap < TimeSpan.Zero)
                            gap = gap.Add(TimeSpan.FromDays(1));

                        var cutoff = schedule.ShiftEnd.Add(TimeSpan.FromTicks(gap.Ticks / 2));

                        _logger.LogInformation(
                            "Night shift: cutoff={Cutoff}, Time={Time}",
                            cutoff,
                            decisionTimestamp.TimeOfDay);

                        if (decisionTimestamp.TimeOfDay < cutoff)
                        {
                            punchType = "Out";
                            scheduleDeterminedPunch = true;
                            _logger.LogInformation("Decision: NIGHT => OUT");
                        }
                        else
                        {
                            punchType = "In";
                            scheduleDeterminedPunch = true;
                            _logger.LogInformation("Decision: NIGHT => IN");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No schedule matched -> using toggle logic");
                }

                // 4) Apply missed-OUT safety only when schedule did NOT determine the punch type
                if (forceInSafety)
                {
                    _logger.LogWarning(
                        "Safety override ACTIVE: scheduleDetermined={ScheduleDetermined}",
                        scheduleDeterminedPunch);
                }

                if (forceInSafety && !scheduleDeterminedPunch)
                {
                    punchType = "In";
                }

                _logger.LogInformation(
                    "FINAL DECISION: EmployeeId={EmployeeId}, PunchType={PunchType}, ScheduleUsed={ScheduleUsed}, SafetyOverride={SafetyOverride}",
                    employee.Id,
                    punchType,
                    scheduleDeterminedPunch,
                    forceInSafety);

                // 5) Save image for new punch, if provided
                string? fileNameForNewPunch = null;
                if (!string.IsNullOrWhiteSpace(request.ImageBase64))
                {
                    fileNameForNewPunch = SavePunchImage(employee.Id, timestamp, request.ImageBase64);
                }

                var punch = new Punch
                {
                    EmployeeId = employee.Id,
                    PunchType = punchType,
                    Timestamp = timestamp,
                    ImageUrl = fileNameForNewPunch ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _repo.AddPunch(punch);

                _logger.LogInformation(
                    "Punch saved for Employee Id {EmployeeId} at {Timestamp} as {PunchType}",
                    employee.Id, timestamp, punchType);

                return Ok(new
                {
                    message = "Punch recorded successfully",
                    timestamp = timestamp,
                    punchType
                });
            }
        }
    }
}
>>>>>>> master
