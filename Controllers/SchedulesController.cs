using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PunchServerMVC.Controllers
{
    public class SchedulesController : Controller
    {
        private readonly IRepository _repo;

        public SchedulesController(IRepository repo)
        {
            _repo = repo;
        }

        private void FillCalendarReturnViewBags(
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            ViewBag.ReturnToCalendar = returnToCalendar;
            ViewBag.CalendarMonth = calendarMonth;
            ViewBag.CalendarYear = calendarYear;
            ViewBag.CalendarEmployeeId = calendarEmployeeId;
            ViewBag.CalendarOrganisationId = calendarOrganisationId;
            ViewBag.CalendarDepartmentId = calendarDepartmentId;
        }

        private IActionResult RedirectAfterScheduleSave(
            bool returnToCalendar,
            int? calendarMonth,
            int? calendarYear,
            int? calendarEmployeeId,
            int? calendarOrganisationId,
            int? calendarDepartmentId)
        {
            if (returnToCalendar)
            {
                return RedirectToAction("Calendar", "Home", new
                {
                    employeeId = calendarEmployeeId,
                    organisationId = calendarOrganisationId,
                    departmentId = calendarDepartmentId,
                    month = calendarMonth,
                    year = calendarYear
                });
            }

            return RedirectToAction(nameof(Index));
        }

        private void FillPunchImpactReturnViewBags(
            bool returnToCalendar,
            int? calendarMonth,
            int? calendarYear,
            int? calendarEmployeeId,
            int? calendarOrganisationId,
            int? calendarDepartmentId)
        {
            ViewBag.ReturnToCalendar = returnToCalendar;
            ViewBag.CalendarMonth = calendarMonth;
            ViewBag.CalendarYear = calendarYear;
            ViewBag.CalendarEmployeeId = calendarEmployeeId;
            ViewBag.CalendarOrganisationId = calendarOrganisationId;
            ViewBag.CalendarDepartmentId = calendarDepartmentId;
        }

        private static Schedule CloneSchedule(Schedule schedule)
        {
            return new Schedule
            {
                EmployeeId = schedule.EmployeeId,
                ScheduleTemplateId = schedule.ScheduleTemplateId,
                ShiftStart = schedule.ShiftStart,
                ShiftEnd = schedule.ShiftEnd,
                BreakMinutes = schedule.BreakMinutes,
                ScheduleType = schedule.ScheduleType,
                Note = schedule.Note,
                StartDate = schedule.StartDate,
                EndDate = schedule.EndDate,
                Days = schedule.Days?.ToList() ?? new List<DayOfWeek>()
            };
        }

        // --------------------------------------------------------------------
        // TIME-AWARE schedule conflict validation (day + night shifts)
        // --------------------------------------------------------------------

        private static bool IsDayActive(Schedule s, DayOfWeek day)
        {
            // If no days specified, treat as "all days"
            return s.Days == null || s.Days.Count == 0 || s.Days.Contains(day);
        }

        private static bool IsNightShift(Schedule s)
        {
            // Night shift crosses midnight (or equal -> treat as night/24h-style)
            return s.ShiftEnd <= s.ShiftStart;
        }

        private static bool IntervalsOverlap((DateTime Start, DateTime End) a, (DateTime Start, DateTime End) b)
        {
            // [Start, End) overlap
            return a.Start < b.End && b.Start < a.End;
        }

        private static List<(DateTime Start, DateTime End)> BuildShiftIntervals(Schedule s, DateTime windowStart, DateTime windowEnd)
        {
            var intervals = new List<(DateTime Start, DateTime End)>();

            var start = windowStart.Date;
            var end = windowEnd.Date;

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                // Only generate intervals on days inside schedule's date range
                if (d < s.StartDate.Date || d > s.EndDate.Date)
                    continue;

                // Only if schedule is active this day
                if (!IsDayActive(s, d.DayOfWeek))
                    continue;

                var shiftStart = d.Add(s.ShiftStart);

                DateTime shiftEnd;
                if (IsNightShift(s))
                {
                    // Cross midnight to next day
                    shiftEnd = d.AddDays(1).Add(s.ShiftEnd);
                }
                else
                {
                    shiftEnd = d.Add(s.ShiftEnd);
                }

                // Ignore zero/negative intervals just in case
                if (shiftEnd > shiftStart)
                    intervals.Add((shiftStart, shiftEnd));
            }

            return intervals;
        }

        private List<Schedule> GetConflictingSchedules(Schedule candidate, int? excludeScheduleId = null)
        {
            var query = _repo.GetSchedules().Where(s => s.EmployeeId == candidate.EmployeeId);

            if (excludeScheduleId.HasValue)
                query = query.Where(s => s.Id != excludeScheduleId.Value);

            var conflicts = new List<Schedule>();

            foreach (var existing in query)
            {
                // quick reject: no date overlap at all
                if (candidate.StartDate.Date > existing.EndDate.Date || candidate.EndDate.Date < existing.StartDate.Date)
                    continue;

                // build a window around the intersection to catch night-spill (prev/next day)
                var overlapStart = (candidate.StartDate.Date > existing.StartDate.Date ? candidate.StartDate.Date : existing.StartDate.Date).AddDays(-1);
                var overlapEnd = (candidate.EndDate.Date < existing.EndDate.Date ? candidate.EndDate.Date : existing.EndDate.Date).AddDays(1);

                var candIntervals = BuildShiftIntervals(candidate, overlapStart, overlapEnd);
                var existIntervals = BuildShiftIntervals(existing, overlapStart, overlapEnd);

                bool hasOverlap = false;

                for (int i = 0; i < candIntervals.Count && !hasOverlap; i++)
                {
                    for (int j = 0; j < existIntervals.Count; j++)
                    {
                        if (IntervalsOverlap(candIntervals[i], existIntervals[j]))
                        {
                            hasOverlap = true;
                            break;
                        }
                    }
                }

                if (hasOverlap)
                    conflicts.Add(existing);
            }

            return conflicts
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.EndDate)
                .ToList();
        }

        private void FillCreateEditViewBags(int? currentEmployeeId = null)
        {
            ViewBag.Templates = _repo.GetTemplates().ToList();

            // ✅ Only ACTIVE employees for schedule creation/edit
            ViewBag.Employees = _repo.GetEmployees()
                .Where(e => e.IsActive || (currentEmployeeId.HasValue && e.Id == currentEmployeeId.Value))
                .ToList();

            // ✅ Department filter support
            ViewBag.Departments = _repo.GetDepartments().ToList();

            var employeeLastSchedules = _repo.GetSchedules()
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.EndDate));

            ViewBag.EmployeeLastSchedules = employeeLastSchedules;
        }

        public IActionResult Index(int? employeeId, string? scheduleType, DateTime? startDate, DateTime? endDate)
        {
            // ✅ Default to CURRENT WEEK (Monday–Sunday) when no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                var today = DateTime.Today;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                startDate = today.AddDays(-diff).Date;     // Monday
                endDate = startDate.Value.AddDays(6).Date; // Sunday
            }

            var allSchedules = _repo.GetSchedules().ToList();
            var employees = _repo.GetEmployees().ToList();

            ViewBag.EmployeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
            ViewBag.AllEmployees = employees;

            ViewBag.ScheduleTypes = allSchedules
                .Select(s => s.ScheduleType)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedScheduleType = scheduleType;
            ViewBag.SelectedStartDate = startDate;
            ViewBag.SelectedEndDate = endDate;

            IEnumerable<Schedule> filtered = allSchedules;

            if (employeeId.HasValue)
                filtered = filtered.Where(s => s.EmployeeId == employeeId.Value);

            if (!string.IsNullOrWhiteSpace(scheduleType))
                filtered = filtered.Where(s =>
                    s.ScheduleType != null &&
                    s.ScheduleType.Equals(scheduleType, StringComparison.OrdinalIgnoreCase));

            // Date period filter (OVERLAP logic): schedule overlaps the [startDate..endDate] interval
            if (startDate.HasValue && endDate.HasValue)
            {
                var s = startDate.Value.Date;
                var e = endDate.Value.Date;
                filtered = filtered.Where(x => x.StartDate.Date <= e && x.EndDate.Date >= s);
            }
            else if (startDate.HasValue)
            {
                var s = startDate.Value.Date;
                filtered = filtered.Where(x => x.EndDate.Date >= s);
            }
            else if (endDate.HasValue)
            {
                var e = endDate.Value.Date;
                filtered = filtered.Where(x => x.StartDate.Date <= e);
            }

            filtered = filtered
                .OrderBy(x => x.EmployeeId)
                .ThenBy(x => x.StartDate)
                .ThenBy(x => x.EndDate);

            return View(filtered.ToList());
        }

        // Optional preselect employee and/or startDate (used from Grid view "Add" button)
        public IActionResult Create(
            int? employeeId,
            DateTime? startDate,
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            FillCreateEditViewBags();
            FillCalendarReturnViewBags(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId ?? employeeId,
                calendarOrganisationId,
                calendarDepartmentId);

            var effectiveStart = startDate?.Date ?? DateTime.Today;
            ViewBag.PreserveStartDate = startDate.HasValue;

            var model = new Schedule
            {
                StartDate = effectiveStart,
                EndDate = GetWeekEndSunday(effectiveStart)
            };

            // ✅ Used by Create.cshtml to preselect one employee inside multi-select
            if (employeeId.HasValue)
                ViewBag.PreselectedEmployeeIds = new List<int> { employeeId.Value };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            Schedule schedule,
            List<int> EmployeeIds,
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            schedule.Note = string.IsNullOrWhiteSpace(schedule.Note) ? null : schedule.Note.Trim();

            // Safe parse Days (if none checked -> empty list)
            schedule.Days = Request.Form["Days"]
                .Select(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            // ✅ Multi-employee support
            EmployeeIds ??= new List<int>();
            EmployeeIds = EmployeeIds.Distinct().ToList();

            if (EmployeeIds.Count == 0)
                ModelState.AddModelError("EmployeeIds", "Please select at least one employee.");

            // If StartDate is empty in form, we will auto-calc per employee.
            var startDateProvided = Request.Form.ContainsKey("StartDate") && !string.IsNullOrWhiteSpace(Request.Form["StartDate"]);

            // If StartDate is not provided, remove binding validation errors so we can auto-calculate.
            if (!startDateProvided)
                ModelState.Remove(nameof(Schedule.StartDate));

            if (startDateProvided && schedule.StartDate > schedule.EndDate)
                ModelState.AddModelError(string.Empty, "Start Date must be before or equal to End Date.");

            // Build last schedule dictionary once (for auto StartDate)
            var employeeLastSchedules = _repo.GetSchedules()
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.EndDate));

            // ✅ Only allow ACTIVE employees to be scheduled
            var employees = _repo.GetEmployees().Where(e => e.IsActive).ToList();
            var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
            var employeeEmploymentStart = employees
                .ToDictionary(e => e.Id, e => e.EmploymentStartDate);

            // If someone posts inactive/unknown ids manually, block it
            var activeIdSet = employees.Select(e => e.Id).ToHashSet();
            foreach (var id in EmployeeIds)
            {
                if (!activeIdSet.Contains(id))
                    ModelState.AddModelError("EmployeeIds", $"Employee #{id} is inactive or not found.");
            }


            // ✅ TIME-AWARE conflict check per employee (date + hours, night shift supported)
            foreach (var empId in EmployeeIds)
    {
        var candidate = new Schedule
        {
            EmployeeId = empId,
            ScheduleTemplateId = schedule.ScheduleTemplateId,
            ShiftStart = schedule.ShiftStart,
            ShiftEnd = schedule.ShiftEnd,
            BreakMinutes = schedule.BreakMinutes,
            ScheduleType = schedule.ScheduleType,
            Note = schedule.Note,
            Days = schedule.Days ?? new List<DayOfWeek>(),
            EndDate = schedule.EndDate
        };

        if (startDateProvided)
        {
            candidate.StartDate = schedule.StartDate;
        }
        else
        {
            // Auto start: last schedule end + 1 day, else employment start, else today
            if (employeeLastSchedules.TryGetValue(empId, out var lastEnd))
                candidate.StartDate = lastEnd.Date.AddDays(1);
            else if (employeeEmploymentStart.TryGetValue(empId, out var empStart) && empStart.HasValue)
                candidate.StartDate = empStart.Value.Date;
            else
                candidate.StartDate = DateTime.Today;
        }

        if (candidate.StartDate > candidate.EndDate)
        {
            var empName = employeeNames.TryGetValue(empId, out var n) ? n : $"Employee #{empId}";
            ModelState.AddModelError(string.Empty, $"{empName}: Start Date must be before or equal to End Date.");
            continue;
        }

        var conflicts = GetConflictingSchedules(candidate, excludeScheduleId: null);
        if (conflicts.Count > 0)
        {
            var empName = employeeNames.TryGetValue(empId, out var n) ? n : $"Employee #{empId}";
            var periods = string.Join("; ", conflicts.Select(c =>
                $"{c.StartDate:dd-MM-yyyy} – {c.EndDate:dd-MM-yyyy} ({c.ShiftStart:hh\\:mm}-{c.ShiftEnd:hh\\:mm})"));

            ModelState.AddModelError(string.Empty,
                $"{empName}: this schedule conflicts (date + hours) with an existing schedule: {periods}");
        }
    }

    if (!ModelState.IsValid)
    {
        FillCreateEditViewBags();
        FillCalendarReturnViewBags(
            returnToCalendar,
            calendarMonth,
            calendarYear,
            calendarEmployeeId,
            calendarOrganisationId,
            calendarDepartmentId);
        schedule.Days ??= new List<DayOfWeek>();
        return View(schedule);
    }

    // ✅ Save schedule(s)
    var created = new List<Schedule>();

    foreach (var empId in EmployeeIds)
    {
        var s = new Schedule
        {
            EmployeeId = empId,
            ScheduleTemplateId = schedule.ScheduleTemplateId,
            ShiftStart = schedule.ShiftStart,
            ShiftEnd = schedule.ShiftEnd,
            BreakMinutes = schedule.BreakMinutes,
            ScheduleType = schedule.ScheduleType,
            Note = schedule.Note,
            Days = schedule.Days ?? new List<DayOfWeek>(),
            EndDate = schedule.EndDate
        };

        if (startDateProvided)
        {
            s.StartDate = schedule.StartDate;
        }
        else
        {
            if (employeeLastSchedules.TryGetValue(empId, out var lastEnd))
                s.StartDate = lastEnd.Date.AddDays(1);
            else if (employeeEmploymentStart.TryGetValue(empId, out var empStart) && empStart.HasValue)
                s.StartDate = empStart.Value.Date;
            else
                s.StartDate = DateTime.Today;
        }

        _repo.AddSchedule(s);
        created.Add(s);
    }

    // Keep your existing punch-impact preview for single-employee create.
    // For multi-create, it can become a large multi-step flow; we skip it and keep schedules saved.
    if (created.Count == 1)
    {
        var impact = BuildPunchTypeImpact(created[0]);
        if (impact.Rows.Count > 0)
        {
            TempData["Warning"] = $"This schedule will change PunchType for {impact.Rows.Count} past AUTO punches. Select which ones to update.";
            FillPunchImpactReturnViewBags(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId,
                calendarOrganisationId,
                calendarDepartmentId);
            return View("PunchTypeImpact", impact);
        }
    }
    else
    {
        TempData["Success"] = $"Created schedules for {created.Count} employees.";
    }

    return RedirectAfterScheduleSave(
        returnToCalendar,
        calendarMonth,
        calendarYear,
        calendarEmployeeId,
        calendarOrganisationId,
        calendarDepartmentId);
}


        public IActionResult Edit(
            int id,
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null) return NotFound();

            FillCreateEditViewBags(schedule.EmployeeId);
            FillCalendarReturnViewBags(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId ?? schedule.EmployeeId,
                calendarOrganisationId,
                calendarDepartmentId);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            int id,
            Schedule model,
            List<DayOfWeek> Days,
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            if (model.StartDate > model.EndDate)
                ModelState.AddModelError(string.Empty, "Start Date must be before or equal to End Date.");

            // Ensure the candidate has the incoming Days for conflict checking
            model.Days = Days ?? new List<DayOfWeek>();

            // ✅ TIME-AWARE conflict check (date + hours, night shift supported)
            var conflicts = GetConflictingSchedules(model, excludeScheduleId: id);
            if (conflicts.Count > 0)
            {
                var periods = string.Join("; ", conflicts.Select(c =>
                    $"{c.StartDate:dd-MM-yyyy} – {c.EndDate:dd-MM-yyyy} ({c.ShiftStart:hh\\:mm}-{c.ShiftEnd:hh\\:mm})"));

                ModelState.AddModelError(string.Empty,
                    $"This schedule conflicts (date + hours) with an existing schedule for the selected employee: {periods}");
            }

            if (!ModelState.IsValid)
            {
                FillCreateEditViewBags(model.EmployeeId);
                FillCalendarReturnViewBags(
                    returnToCalendar,
                    calendarMonth,
                    calendarYear,
                    calendarEmployeeId ?? model.EmployeeId,
                    calendarOrganisationId,
                    calendarDepartmentId);
                model.Days ??= new List<DayOfWeek>();
                return View(model);
            }

            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null) return NotFound();

            schedule.EmployeeId = model.EmployeeId;
            schedule.ScheduleTemplateId = model.ScheduleTemplateId;
            schedule.ShiftStart = model.ShiftStart;
            schedule.ShiftEnd = model.ShiftEnd;
            schedule.BreakMinutes = model.BreakMinutes;
            schedule.ScheduleType = model.ScheduleType;
            schedule.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
            schedule.StartDate = model.StartDate;
            schedule.EndDate = model.EndDate;
            schedule.Days = Days;

            _repo.UpdateSchedule(schedule);

            var impact = BuildPunchTypeImpact(schedule);
            if (impact.Rows.Count > 0)
            {
                TempData["Warning"] = $"This schedule change will update PunchType for {impact.Rows.Count} past AUTO punches. Select which ones to apply.";
                FillPunchImpactReturnViewBags(
                    returnToCalendar,
                    calendarMonth,
                    calendarYear,
                    calendarEmployeeId ?? schedule.EmployeeId,
                    calendarOrganisationId,
                    calendarDepartmentId);
                return View("PunchTypeImpact", impact);
            }

            return RedirectAfterScheduleSave(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId ?? schedule.EmployeeId,
                calendarOrganisationId,
                calendarDepartmentId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            _repo.DeleteSchedule(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteDayFromCalendar(
            int id,
            DateTime selectedDate,
            int? calendarMonth,
            int? calendarYear,
            int? calendarEmployeeId,
            int? calendarOrganisationId,
            int? calendarDepartmentId)
        {
            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null)
            {
                TempData["Error"] = "Schedule not found.";
                return RedirectToAction("Calendar", "Home", new
                {
                    employeeId = calendarEmployeeId,
                    organisationId = calendarOrganisationId,
                    departmentId = calendarDepartmentId,
                    month = calendarMonth,
                    year = calendarYear
                });
            }

            var day = selectedDate.Date;
            if (day < schedule.StartDate.Date || day > schedule.EndDate.Date)
            {
                TempData["Error"] = "Selected day is outside the schedule range.";
                return RedirectToAction("Calendar", "Home", new
                {
                    employeeId = calendarEmployeeId ?? schedule.EmployeeId,
                    organisationId = calendarOrganisationId,
                    departmentId = calendarDepartmentId,
                    month = calendarMonth,
                    year = calendarYear
                });
            }

            if (schedule.StartDate.Date == schedule.EndDate.Date && schedule.StartDate.Date == day)
            {
                _repo.DeleteSchedule(schedule.Id);
            }
            else if (day == schedule.StartDate.Date)
            {
                schedule.StartDate = schedule.StartDate.Date.AddDays(1);
                _repo.UpdateSchedule(schedule);
            }
            else if (day == schedule.EndDate.Date)
            {
                schedule.EndDate = schedule.EndDate.Date.AddDays(-1);
                _repo.UpdateSchedule(schedule);
            }
            else
            {
                var trailingPart = CloneSchedule(schedule);
                trailingPart.StartDate = day.AddDays(1);
                trailingPart.EndDate = schedule.EndDate.Date;

                schedule.EndDate = day.AddDays(-1);

                _repo.UpdateSchedule(schedule);
                _repo.AddSchedule(trailingPart);
            }

            TempData["Success"] = $"Schedule removed for {day:dd.MM.yyyy}.";
            return RedirectToAction("Calendar", "Home", new
            {
                employeeId = calendarEmployeeId ?? schedule.EmployeeId,
                organisationId = calendarOrganisationId,
                departmentId = calendarDepartmentId,
                month = calendarMonth ?? day.Month,
                year = calendarYear ?? day.Year
            });
        }

        private static DateTime GetWeekEndSunday(DateTime date)
        {
            int diff = (7 - (int)date.DayOfWeek) % 7;
            return date.Date.AddDays(diff);
        }
        private static DateTime GetWeekStartMonday(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        public IActionResult Grid(DateTime? weekStart, int? employeeId, string? scheduleType, int? departmentId, string? scheduleStatus)
        {
            var ws = weekStart?.Date ?? GetWeekStartMonday(DateTime.Today);
            ws = GetWeekStartMonday(ws);
            var we = ws.AddDays(6);

            // ✅ show ACTIVE employees only
            var allEmployees = _repo.GetEmployees()
                .Where(e => e.IsActive && !e.DoesNotNeedSchedule)
                .ToList();
            var allSchedules = _repo.GetSchedules().ToList();

            ViewBag.ScheduleTypes = allSchedules
                .Select(s => s.ScheduleType)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var filteredSchedules = allSchedules
                .Where(s => s.StartDate.Date <= we && s.EndDate.Date >= ws);

            if (!string.IsNullOrWhiteSpace(scheduleType))
                filteredSchedules = filteredSchedules.Where(s =>
                    s.ScheduleType != null &&
                    s.ScheduleType.Equals(scheduleType, StringComparison.OrdinalIgnoreCase));

            // ✅ "has schedule" map AFTER week + scheduleType filters
            var hasScheduleInView = filteredSchedules
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Any());

            // employee filter for displayed schedules only (after hasSchedule map)
            if (employeeId.HasValue)
                filteredSchedules = filteredSchedules.Where(s => s.EmployeeId == employeeId.Value);

            ViewBag.Schedules = filteredSchedules.ToList();

            ViewBag.WeekStart = ws;
            ViewBag.WeekEnd = we;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedScheduleType = scheduleType;
            ViewBag.SelectedScheduleStatus = string.IsNullOrWhiteSpace(scheduleStatus) ? "all" : scheduleStatus;
            ViewBag.AllEmployees = allEmployees;

            var employeesToShow = allEmployees
                .Where(e =>
                    (!e.EmploymentStartDate.HasValue || e.EmploymentStartDate.Value.Date <= we) &&
                    (!e.EmploymentEndDate.HasValue || e.EmploymentEndDate.Value.Date >= ws))
                .AsEnumerable();

            if (departmentId.HasValue)
                employeesToShow = employeesToShow.Where(e => e.DepartmentId == departmentId.Value);

            if (employeeId.HasValue)
                employeesToShow = employeesToShow.Where(e => e.Id == employeeId.Value);

            // ✅ Filter employees by schedule presence in selected week (and scheduleType filter)
            if (!string.IsNullOrWhiteSpace(scheduleStatus))
            {
                var status = scheduleStatus.Trim().ToLowerInvariant();
                if (status == "with")
                    employeesToShow = employeesToShow.Where(e => hasScheduleInView.ContainsKey(e.Id));
                else if (status == "without")
                    employeesToShow = employeesToShow.Where(e => !hasScheduleInView.ContainsKey(e.Id));
            }

            var departments = _repo.GetDepartments().ToList();
            ViewBag.Departments = departments;
            ViewBag.SelectedDepartmentId = departmentId;
            ViewBag.HolidaysInWeek = _repo.GetHolidaysInRange(ws, we)
                .Select(h => h.Date.Date)
                .ToHashSet();

            return View(employeesToShow.ToList());
        }


        public IActionResult WeeklyGrid(DateTime? weekStart, int? employeeId, string? scheduleType)
        {
            return RedirectToAction(nameof(Grid), new { weekStart, employeeId, scheduleType });
        }

        // --------------------------------------------------------------------
        // PunchType impact preview + apply (AUTO punches ONLY)
        // --------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApplyPunchTypeUpdates(
            int scheduleId,
            List<int> selectedPunchIds,
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return NotFound();

            if (selectedPunchIds == null || selectedPunchIds.Count == 0)
            {
                TempData["Info"] = "No punches selected. Schedule saved; no punch types were changed.";
                return RedirectAfterScheduleSave(
                    returnToCalendar,
                    calendarMonth,
                    calendarYear,
                    calendarEmployeeId ?? schedule.EmployeeId,
                    calendarOrganisationId,
                    calendarDepartmentId);
            }

            var margin = TimeSpan.FromMinutes(15);

            var from = schedule.StartDate.Date;
            var toExclusive = schedule.EndDate.Date.AddDays(2);

            var punches = _repo.GetPunches()
                .Where(p => p.EmployeeId == schedule.EmployeeId)
                .Where(p => p.Timestamp >= from && p.Timestamp < toExclusive)
                .ToList();

            punches = punches
                .Where(p => IsScheduleMatchForTimestamp(schedule, p.Timestamp, margin))
                .ToList();

            var proposedMap = BuildProposedTypeMap(schedule, punches, margin);

            var selected = punches
                .Where(p => selectedPunchIds.Contains(p.Id))
                .ToList();

            int updated = 0;

            foreach (var p in selected)
            {
                if (!IsAutoPunch(p)) continue;

                if (!proposedMap.TryGetValue(p.Id, out var proposed))
                    continue;

                if (!string.Equals(p.PunchType ?? string.Empty, proposed.proposedType, StringComparison.OrdinalIgnoreCase))
                {
                    p.PunchType = proposed.proposedType;
                    _repo.UpdatePunch(p);
                    updated++;
                }
            }

            TempData["Success"] = $"Updated PunchType for {updated} AUTO punches.";
            return RedirectAfterScheduleSave(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId ?? schedule.EmployeeId,
                calendarOrganisationId,
                calendarDepartmentId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SkipPunchTypeUpdates(
            bool returnToCalendar = false,
            int? calendarMonth = null,
            int? calendarYear = null,
            int? calendarEmployeeId = null,
            int? calendarOrganisationId = null,
            int? calendarDepartmentId = null)
        {
            TempData["Info"] = "Schedule saved. No punch types were changed.";
            return RedirectAfterScheduleSave(
                returnToCalendar,
                calendarMonth,
                calendarYear,
                calendarEmployeeId,
                calendarOrganisationId,
                calendarDepartmentId);
        }

        private static bool IsAutoPunch(Punch p)
        {
            return p.IsManualCreated == false && p.IsManualEdited == false;
        }

        private static DateTime GetEffectiveWorkDate(Schedule s, DateTime ts, TimeSpan margin)
        {
            bool isNightShift = s.ShiftEnd < s.ShiftStart;

            if (!isNightShift)
                return ts.Date;

            // Same workday split logic as PunchesController:
            // Example night shift 18:00 -> 09:00
            // Gap between end and next start = 09:00 -> 18:00 = 9h
            // Midpoint cutoff = 13:30
            // Times before 13:30 belong to previous workday.
            var gap = s.ShiftStart - s.ShiftEnd;
            if (gap < TimeSpan.Zero)
                gap = gap.Add(TimeSpan.FromDays(1));

            var cutoff = s.ShiftEnd + TimeSpan.FromTicks(gap.Ticks / 2);

            if (ts.TimeOfDay < cutoff)
                return ts.AddDays(-1).Date;

            return ts.Date;
        }

        private static bool IsScheduleMatchForTimestamp(Schedule s, DateTime ts, TimeSpan margin)
        {
            var effectiveDate = GetEffectiveWorkDate(s, ts, margin);
            var effectiveDay = effectiveDate.DayOfWeek;

            // If Days empty -> treat as all days
            if (s.Days != null && s.Days.Count > 0 && !s.Days.Contains(effectiveDay))
                return false;

            if (s.StartDate.Date > effectiveDate) return false;
            if (s.EndDate.Date < effectiveDate) return false;

            return true;
        }

        private static Dictionary<int, (string proposedType, string reason)> BuildProposedTypeMap(
    Schedule schedule,
    List<Punch> punches,
    TimeSpan margin)
        {
            var map = new Dictionary<int, (string proposedType, string reason)>();

            bool isNightShift = schedule.ShiftEnd < schedule.ShiftStart;

            foreach (var p in punches.OrderBy(x => x.Timestamp))
            {
                string proposedType;
                string reason;

                if (!isNightShift)
                {
                    // Day shift:
                    // first half => In
                    // second half => Out
                    var shiftLength = schedule.ShiftEnd - schedule.ShiftStart;
                    var midpoint = schedule.ShiftStart.Add(TimeSpan.FromTicks(shiftLength.Ticks / 2));

                    if (p.Timestamp.TimeOfDay < midpoint)
                    {
                        proposedType = "In";
                        reason = $"Day shift: before midpoint {midpoint:hh\\:mm}";
                    }
                    else
                    {
                        proposedType = "Out";
                        reason = $"Day shift: from midpoint {midpoint:hh\\:mm}";
                    }
                }
                else
                {
                    // Night shift:
                    // use the SAME cutoff as GetEffectiveWorkDate
                    // Example 18:00 -> 09:00:
                    // gap = 9h, cutoff = 13:30
                    // times before 13:30 => morning side => Out
                    // times from 13:30 onward => evening side => In
                    var gap = schedule.ShiftStart - schedule.ShiftEnd;
                    if (gap < TimeSpan.Zero)
                        gap = gap.Add(TimeSpan.FromDays(1));

                    var cutoff = schedule.ShiftEnd + TimeSpan.FromTicks(gap.Ticks / 2);

                    if (p.Timestamp.TimeOfDay < cutoff)
                    {
                        proposedType = "Out";
                        reason = $"Night shift: morning side (< {cutoff:hh\\:mm})";
                    }
                    else
                    {
                        proposedType = "In";
                        reason = $"Night shift: evening side (>= {cutoff:hh\\:mm})";
                    }
                }

                map[p.Id] = (proposedType, reason);
            }

            return map;
        }

        private SchedulePunchTypeImpactViewModel BuildPunchTypeImpact(Schedule schedule)
        {
            var employees = _repo.GetEmployees().ToList();
            var emp = employees.FirstOrDefault(e => e.Id == schedule.EmployeeId);

            var from = schedule.StartDate.Date;
            var toExclusive = schedule.EndDate.Date.AddDays(2);

            var margin = TimeSpan.FromMinutes(15);

            var punches = _repo.GetPunches()
                .Where(p => p.EmployeeId == schedule.EmployeeId)
                .Where(p => p.Timestamp >= from && p.Timestamp < toExclusive)
                .ToList();

            punches = punches
                .Where(p => IsScheduleMatchForTimestamp(schedule, p.Timestamp, margin))
                .ToList();

            var proposedMap = BuildProposedTypeMap(schedule, punches, margin);

            var rows = new List<PunchTypeImpactRow>();

            foreach (var p in punches.OrderBy(x => x.Timestamp))
            {
                if (!IsAutoPunch(p)) continue;

                var current = p.PunchType ?? string.Empty;

                if (!proposedMap.TryGetValue(p.Id, out var proposed))
                    continue;

                if (!string.Equals(current, proposed.proposedType, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(new PunchTypeImpactRow
                    {
                        PunchId = p.Id,
                        EmployeeId = p.EmployeeId,
                        EmployeeName = emp?.FullName ?? $"Employee #{p.EmployeeId}",
                        Timestamp = p.Timestamp,
                        CurrentType = current,
                        ProposedType = proposed.proposedType,
                        IsAuto = true,
                        Reason = proposed.reason
                    });
                }
            }

            return new SchedulePunchTypeImpactViewModel
            {
                ScheduleId = schedule.Id,
                EmployeeId = schedule.EmployeeId,
                EmployeeName = emp?.FullName ?? $"Employee #{schedule.EmployeeId}",
                StartDate = schedule.StartDate,
                EndDate = schedule.EndDate,
                ShiftStart = schedule.ShiftStart,
                ShiftEnd = schedule.ShiftEnd,
                ScheduleType = schedule.ScheduleType,
                Rows = rows
            };
        }
    }
}
