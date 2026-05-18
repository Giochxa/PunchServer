using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PunchServerMVC.Data;
using PunchServerMVC.Models;

namespace PunchServerMVC.Services
{
    public class AutoPunchOutWorker : BackgroundService
    {
        private readonly ILogger<AutoPunchOutWorker> _logger;
        private readonly IRepository _repo;

        public AutoPunchOutWorker(ILogger<AutoPunchOutWorker> logger, IRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RunIfDue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto Punch Out worker failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private void RunIfDue()
        {
            var settings = _repo.GetAdministrationSettings();
            if (!settings.AutoPunchOutEnabled || !settings.AutoPunchOutTimeCheck.HasValue)
                return;

            var now = DateTime.Now;
            if (now.TimeOfDay < settings.AutoPunchOutTimeCheck.Value)
                return;

            if (settings.LastAutoPunchOutRunAt.HasValue &&
                settings.LastAutoPunchOutRunAt.Value.Date == now.Date)
            {
                var lastRunCheckTime = settings.LastAutoPunchOutRunTimeCheck;
                if (lastRunCheckTime.HasValue &&
                    lastRunCheckTime.Value >= settings.AutoPunchOutTimeCheck.Value)
                {
                    return;
                }
            }

            var runLog = ExecuteRun(now);
            settings.LastAutoPunchOutRunAt = runLog.FinishedAt;
            settings.LastAutoPunchOutRunTimeCheck = settings.AutoPunchOutTimeCheck;
            _repo.SaveAdministrationSettings(settings);
            _repo.AddAutoPunchOutRunLog(runLog);
        }

        private AutoPunchOutRunLog ExecuteRun(DateTime runAt)
        {
            var startedAt = DateTime.Now;
            var log = new AutoPunchOutRunLog
            {
                StartedAt = startedAt,
                Status = "Running"
            };

            try
            {
                var windowStart = runAt.AddHours(-24);
                var employees = _repo.GetEmployees()
                    .Where(e => e.IsActive)
                    .ToDictionary(e => e.Id);

                var employeeIds = employees.Keys.ToHashSet();
                var punches = _repo.GetAllPunches()
                    .Where(p => employeeIds.Contains(p.EmployeeId) &&
                                p.Timestamp >= windowStart.AddHours(-12) &&
                                p.Timestamp <= runAt.AddHours(1))
                    .OrderBy(p => p.Timestamp)
                    .ToList();

                var schedules = _repo.GetSchedules()
                    .Where(s => employeeIds.Contains(s.EmployeeId) &&
                                s.StartDate.Date <= runAt.Date &&
                                s.EndDate.Date >= windowStart.Date.AddDays(-1))
                    .ToList();

                foreach (var schedule in schedules)
                {
                    foreach (var workDate in EachDate(windowStart.Date.AddDays(-1), runAt.Date))
                    {
                        if (!MatchesScheduleDay(schedule, workDate))
                            continue;

                        var shiftStart = workDate.Date + schedule.ShiftStart;
                        var isNightShift = schedule.ShiftEnd < schedule.ShiftStart;
                        var shiftEnd = isNightShift
                            ? workDate.Date.AddDays(1) + schedule.ShiftEnd
                            : workDate.Date + schedule.ShiftEnd;

                        if (shiftEnd < windowStart || shiftEnd > runAt)
                            continue;

                        var shiftPunches = punches
                            .Where(p => p.EmployeeId == schedule.EmployeeId &&
                                        p.Timestamp >= shiftStart.AddHours(-4) &&
                                        p.Timestamp <= shiftEnd.AddHours(12))
                            .OrderBy(p => p.Timestamp)
                            .ToList();

                        var inPunch = shiftPunches
                            .FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase));

                        if (inPunch == null)
                            continue;

                        var outPunch = shiftPunches
                            .FirstOrDefault(p =>
                                p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase) &&
                                p.Timestamp > inPunch.Timestamp);

                        if (outPunch != null)
                            continue;

                        var employee = employees[schedule.EmployeeId];
                        var autoOutPunch = new Punch
                        {
                            EmployeeId = schedule.EmployeeId,
                            PunchType = "Out",
                            Timestamp = shiftEnd,
                            CreatedAt = runAt,
                            IsManualCreated = false,
                            IsManualEdited = false,
                            IsAutoPunchOut = true,
                            Note = "Auto Punch Out"
                        };

                        _repo.AddPunch(autoOutPunch);
                        punches.Add(autoOutPunch);

                        log.AddedPunches.Add(new AutoPunchOutRunLogItem
                        {
                            PunchId = autoOutPunch.Id,
                            EmployeeId = employee.Id,
                            EmployeeName = employee.FullName,
                            PunchTimestamp = autoOutPunch.Timestamp,
                            WorkDate = workDate.Date,
                            ScheduleText = $"{schedule.ShiftStart:hh\\:mm} - {schedule.ShiftEnd:hh\\:mm}"
                        });
                    }
                }

                log.Status = "Success";
                log.Message = log.AddedPunches.Any()
                    ? $"Added {log.AddedPunches.Count} auto Out punch(es)."
                    : "No missing Out punches found.";
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.Message = ex.Message;
                _logger.LogError(ex, "Auto Punch Out worker run failed.");
            }

            log.FinishedAt = DateTime.Now;
            return log;
        }

        private static IEnumerable<DateTime> EachDate(DateTime fromInclusive, DateTime toInclusive)
        {
            for (var date = fromInclusive.Date; date <= toInclusive.Date; date = date.AddDays(1))
                yield return date;
        }

        private static bool MatchesScheduleDay(Schedule schedule, DateTime date)
        {
            if (schedule.StartDate.Date > date.Date || schedule.EndDate.Date < date.Date)
                return false;

            if (schedule.Days == null || schedule.Days.Count == 0)
                return true;

            return schedule.Days.Contains(date.DayOfWeek);
        }
    }
}
