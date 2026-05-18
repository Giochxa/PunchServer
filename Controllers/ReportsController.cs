<<<<<<< HEAD
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IRepository _repo;

        public ReportsController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult MonthlyPunchReport(
            int? organisationId,
            int? departmentId,
            int? employeeId,
            int? year,
            int? month,
            int? day)
        {
            var vm = new MonthlyPunchReportViewModel
            {
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments(),
                Employees = _repo.GetEmployees().ToList(),
                OrganisationId = organisationId,
                DepartmentId = departmentId,
                EmployeeId = employeeId,
                Year = year ?? DateTime.Now.Year,
                Month = month ?? DateTime.Now.Month,
            };
            vm.Day = day ?? DateTime.Now.Day;

            // Which employees should appear as rows?
            var employeesQuery = vm.Employees.AsEnumerable();

            if (organisationId.HasValue)
                employeesQuery = employeesQuery.Where(e => e.OrganisationId == organisationId.Value);

            if (departmentId.HasValue)
                employeesQuery = employeesQuery.Where(e => e.DepartmentId == departmentId.Value);

            if (employeeId.HasValue)
                employeesQuery = employeesQuery.Where(e => e.Id == employeeId.Value);

            var employeesForRows = employeesQuery.ToList();

            if (!employeesForRows.Any())
                return View(vm);

            var monthStart = new DateTime(vm.Year, vm.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            int daysInMonth = DateTime.DaysInMonth(vm.Year, vm.Month);
            int startDay = vm.Day ?? 1;
            int endDay = vm.Day ?? daysInMonth;

            // Get punches for this month only
            var punches = _repo.GetPunches()
                .Where(p => p.Timestamp.Date >= monthStart && p.Timestamp.Date <= monthEnd)
                .ToList();

            // Lookups for names
            var orgLookup = vm.Organisations.ToDictionary(o => o.Id, o => o.Name);
            var deptLookup = vm.Departments.ToDictionary(d => d.Id, d => d.Name);

            foreach (var emp in employeesForRows)
            {
                var empPunches = punches
                    .Where(p => p.EmployeeId == emp.Id)
                    .OrderBy(p => p.Timestamp)
                    .ToList();

                if (!empPunches.Any())
                    continue;

                // Load all schedules for the month
                var employeeSchedules = _repo.GetSchedules()
                    .Where(s =>
                        s.EmployeeId == emp.Id &&
                        s.StartDate <= monthEnd &&
                        s.EndDate >= monthStart)
                    .ToList();

                var row = new MonthlyPunchRow
                {
                    OrganisationName = emp.OrganisationId.HasValue && orgLookup.TryGetValue(emp.OrganisationId.Value, out var oname) ? oname : "",
                    DepartmentName = emp.DepartmentId.HasValue && deptLookup.TryGetValue(emp.DepartmentId.Value, out var dname) ? dname : "",
                    EmployeeName = emp.FullName,
                    PersonalId = emp.PersonalId
                };

                for (int d = startDay; d <= endDay; d++)
                {
                    var date = new DateTime(vm.Year, vm.Month, d);

                    var schedule = employeeSchedules.FirstOrDefault(s =>
                        s.Days.Contains(date.DayOfWeek) &&
                        s.StartDate <= date &&
                        s.EndDate >= date);

                    bool isNightShift = schedule != null && schedule.ShiftEnd < schedule.ShiftStart;

                    // punches on this date
                    var todaysPunches = empPunches
                        .Where(p => p.Timestamp.Date == date.Date)
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    // include morning punches next day ONLY if night shift
                    if (isNightShift)
                    {
                        var nextMorningPunches = empPunches
                            .Where(p =>
                                p.Timestamp.Date == date.AddDays(1).Date &&
                                p.Timestamp.TimeOfDay <= TimeSpan.FromHours(12))
                            .OrderBy(p => p.Timestamp)
                            .ToList();

                        todaysPunches.AddRange(nextMorningPunches);
                    }

                    todaysPunches = todaysPunches.OrderBy(p => p.Timestamp).ToList();

                    DateTime? punchIn = null;
                    DateTime? punchOut = null;
                    double? diff = null;

                    if (todaysPunches.Any())
                    {
                        punchIn = todaysPunches
                            .FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase))
                            ?.Timestamp ?? todaysPunches.First().Timestamp;

                        punchOut = todaysPunches
                            .LastOrDefault(p => p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase))
                            ?.Timestamp;

                        // OUT cannot be before or equal to IN
                        if (punchOut.HasValue && punchOut.Value <= punchIn.Value)
                        {
                            punchOut = null;
                        }

                        if (punchIn.HasValue && punchOut.HasValue)
                        {
                            diff = (punchOut.Value - punchIn.Value).TotalHours;

                            // remove invalid/zero durations
                            if (diff <= 0.01)
                            {
                                diff = null;
                                punchOut = null;
                            }
                        }
                    }

                    row.Days.Add(new DailyPunch
                    {
                        PunchIn = punchIn,
                        PunchOut = punchOut,
                        DifferenceHours = diff
                    });
                }

                vm.Rows.Add(row);
            }

            return View(vm);
        }
    }
}
=======
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IRepository _repo;

        public ReportsController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult MonthlyPunchReport(
            string? activeTab,
            int? organisationId,
            int? departmentId,
            int? employeeId,
            int? year,
            int? month,
            int? day,
            double? minDifferenceHours,
            DateTime? vacationFrom,
            DateTime? vacationTo,
            DateTime? lateFrom,
            DateTime? lateTo,
            int? lateInMinutes,
            int? earlyOutMinutes,
            int? overtimeMinutes,
            string? salaryMonthValue)
        {
            // ✅ Sanitize date inputs (prevents ArgumentOutOfRangeException)
            var now = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(salaryMonthValue) && salaryMonthValue.Length >= 7)
            {
                if (int.TryParse(salaryMonthValue.Substring(0, 4), out var parsedYear))
                    year = parsedYear;
                if (int.TryParse(salaryMonthValue.Substring(5, 2), out var parsedMonth))
                    month = parsedMonth;
            }

            int safeYear = year ?? now.Year;
            if (safeYear < 1) safeYear = 1;
            if (safeYear > 9999) safeYear = 9999;

            int safeMonth = month ?? now.Month;
            if (safeMonth < 1) safeMonth = 1;
            if (safeMonth > 12) safeMonth = 12;

            int daysInMonth = DateTime.DaysInMonth(safeYear, safeMonth);

            // If day is provided but invalid (e.g. 31 in Feb), clamp it into the valid range
            int? safeDay = day;
            if (safeDay.HasValue)
            {
                if (safeDay.Value < 1) safeDay = 1;
                if (safeDay.Value > daysInMonth) safeDay = daysInMonth;
            }

            var vm = new MonthlyPunchReportViewModel
            {
                ActiveTab = string.IsNullOrWhiteSpace(activeTab) ? "monthly" : activeTab,
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments(),
                Employees = _repo.GetEmployees().ToList(),
                OrganisationId = organisationId,
                DepartmentId = departmentId,
                EmployeeId = employeeId,
                Year = safeYear,
                Month = safeMonth,
                Day = safeDay,
                MinDifferenceHours = minDifferenceHours,
                VacationFrom = (vacationFrom ?? new DateTime(now.Year, 1, 1)).Date,
                VacationTo = (vacationTo ?? new DateTime(now.Year, 12, 31)).Date,
                LateFrom = (lateFrom ?? new DateTime(now.Year, now.Month, 1)).Date,
                LateTo = (lateTo ?? new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))).Date,
                LateInMinutes = lateInMinutes,
                EarlyOutMinutes = earlyOutMinutes,
                OvertimeMinutes = overtimeMinutes
            };

            var employees = vm.Employees.AsEnumerable();
            if (organisationId.HasValue)
                employees = employees.Where(e => e.OrganisationId == organisationId);
            if (departmentId.HasValue)
                employees = employees.Where(e => e.DepartmentId == departmentId);
            if (employeeId.HasValue)
                employees = employees.Where(e => e.Id == employeeId);

            var monthStart = new DateTime(vm.Year, vm.Month, 1);
            var monthEndExclusive = monthStart.AddMonths(1);

            int startDay = vm.Day ?? 1;
            int endDay = vm.Day ?? DateTime.DaysInMonth(vm.Year, vm.Month);

            // Extra safety (in case something changes later)
            int maxDay = DateTime.DaysInMonth(vm.Year, vm.Month);
            if (startDay < 1) startDay = 1;
            if (startDay > maxDay) startDay = maxDay;
            if (endDay < 1) endDay = 1;
            if (endDay > maxDay) endDay = maxDay;
            if (endDay < startDay) endDay = startDay;

            var reportStartDate = new DateTime(vm.Year, vm.Month, startDay);
            var reportEndDate = new DateTime(vm.Year, vm.Month, endDay);

            employees = employees.Where(e =>
                !e.DoesNotNeedSchedule &&
                (!e.EmploymentStartDate.HasValue || e.EmploymentStartDate.Value.Date <= reportEndDate.Date) &&
                (!e.EmploymentEndDate.HasValue || e.EmploymentEndDate.Value.Date >= reportStartDate.Date));

            var employeeList = employees.ToList();

            if (vm.ActiveTab == "vacations")
            {
                vm.VacationRows = BuildVacationRows(employeeList, vm);
                return View(vm);
            }

            if (vm.ActiveTab == "late")
            {
                vm.LateRows = BuildLateRows(employeeList, vm);
                return View(vm);
            }

            if (vm.ActiveTab == "salary")
            {
                vm.SalaryRows = BuildSalaryRows(employeeList, vm);
                return View(vm);
            }

            if (!employeeList.Any())
                return View(vm);

            var loadStart = reportStartDate.AddDays(-1);
            var loadEndExclusive = monthEndExclusive.AddDays(1);

            var punches = _repo.GetPunches()
                .Where(p => p.Timestamp >= loadStart && p.Timestamp < loadEndExclusive)
                .OrderBy(p => p.Timestamp)
                .ToList();

            foreach (var emp in employeeList)
            {
                var empPunches = punches.Where(p => p.EmployeeId == emp.Id).ToList();

                var row = new MonthlyPunchRow
                {
                    OrganisationName = vm.Organisations.FirstOrDefault(o => o.Id == emp.OrganisationId)?.Name ?? "",
                    DepartmentName = vm.Departments.FirstOrDefault(d => d.Id == emp.DepartmentId)?.Name ?? "",
                    EmployeeName = emp.FullName,
                    PersonalId = emp.PersonalId
                };

                var usedOutPunches = new HashSet<int>();

                // Seed: consume cross-day OUT that belongs to previous day
                {
                    var prevDate = reportStartDate.AddDays(-1);

                    var prevDayPunches = empPunches
                        .Where(p => p.Timestamp.Date == prevDate.Date)
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    var prevIn = prevDayPunches
                        .FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase));

                    if (prevIn != null)
                    {
                        bool prevHasAnyOutSameDay = prevDayPunches
                            .Any(p => p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase));

                        if (!prevHasAnyOutSameDay)
                        {
                            var outAfterPrevIn = empPunches
                                .Where(p =>
                                    p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase) &&
                                    p.Timestamp > prevIn.Timestamp)
                                .OrderBy(p => p.Timestamp)
                                .FirstOrDefault();

                            if (outAfterPrevIn != null && outAfterPrevIn.Timestamp.Date == reportStartDate.Date)
                                usedOutPunches.Add(outAfterPrevIn.Id);
                        }
                    }
                }

                for (int d = startDay; d <= endDay; d++)
                {
                    // Now guaranteed valid
                    var date = new DateTime(vm.Year, vm.Month, d);

                    var todayPunches = empPunches
                        .Where(p => p.Timestamp.Date == date.Date)
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    DateTime? punchIn = null;
                    DateTime? punchOut = null;

                    var inPunch = todayPunches
                        .FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase));

                    if (inPunch != null)
                        punchIn = inPunch.Timestamp;

                    var outPunchToday = todayPunches
                        .Where(p =>
                            p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase) &&
                            !usedOutPunches.Contains(p.Id))
                        .LastOrDefault();

                    if (outPunchToday != null)
                    {
                        punchOut = outPunchToday.Timestamp;
                        usedOutPunches.Add(outPunchToday.Id);
                    }
                    else if (punchIn.HasValue)
                    {
                        var nextOut = empPunches
                            .Where(p =>
                                p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase) &&
                                p.Timestamp > punchIn.Value &&
                                !usedOutPunches.Contains(p.Id))
                            .OrderBy(p => p.Timestamp)
                            .FirstOrDefault();

                        if (nextOut != null)
                        {
                            punchOut = nextOut.Timestamp;
                            usedOutPunches.Add(nextOut.Id);
                        }
                    }

                    double? diff = null;
                    if (punchIn.HasValue && punchOut.HasValue)
                        diff = Math.Round((punchOut.Value - punchIn.Value).TotalHours, 2);

                    row.Days.Add(new DailyPunch
                    {
                        PunchIn = punchIn,
                        PunchOut = punchOut,
                        DifferenceHours = diff
                    });
                }

                bool passesDifferenceFilter = !vm.MinDifferenceHours.HasValue
                || row.Days.Any(d => d.DifferenceHours.HasValue && d.DifferenceHours.Value > vm.MinDifferenceHours.Value);

                if (passesDifferenceFilter)
                {
                    vm.Rows.Add(row);
                }
            }

            return View(vm);
        }

        private List<VacationReportRow> BuildVacationRows(List<Employee> employees, MonthlyPunchReportViewModel vm)
        {
            var departments = vm.Departments.ToDictionary(d => d.Id, d => d.Name);
            var departmentsById = vm.Departments.ToDictionary(d => d.Id);
            var reportYear = vm.VacationFrom.Year;
            var vacationTypesById = _repo.GetVacationTypes().ToDictionary(v => v.Id);

            return employees.Select(employee =>
                {
                    var usedDays = _repo.GetVacations()
                        .Where(v => v.EmployeeId == employee.Id && v.IsActive)
                        .Where(v => !vacationTypesById.TryGetValue(v.VacationTypeId, out var type) || !type.DoesNotCountAsUsedVacations)
                        .Select(v =>
                        {
                            var overlapStart = v.StartDate.Date < vm.VacationFrom.Date ? vm.VacationFrom.Date : v.StartDate.Date;
                            var overlapEnd = v.EndDate.Date > vm.VacationTo.Date ? vm.VacationTo.Date : v.EndDate.Date;
                            return overlapEnd < overlapStart ? 0 : (overlapEnd - overlapStart).Days + 1;
                        })
                        .Sum();

                    var employmentStartYear = employee.EmploymentStartDate?.Year;
                    var remainingVacationsFromYear = employee.RemainingVacationsFrom?.Year;
                    var baseAnnualLimit =
                        employee.VacationsPerYear ??
                        (employee.DepartmentId.HasValue && departmentsById.TryGetValue(employee.DepartmentId.Value, out var department)
                            ? department.VacationsPerYear
                            : null) ??
                        24;

                    var annualLimit =
                        remainingVacationsFromYear.HasValue &&
                        remainingVacationsFromYear.Value == reportYear &&
                        employee.RemainingVacations.HasValue
                            ? employee.RemainingVacations.Value
                            : employmentStartYear.HasValue &&
                              employmentStartYear.Value == reportYear &&
                              employee.RemainingVacations.HasValue
                                ? employee.RemainingVacations.Value
                            : baseAnnualLimit;

                    var remaining = Math.Max(annualLimit - usedDays, 0);

                    return new VacationReportRow
                    {
                        EmployeeName = employee.FullName,
                        DepartmentName = employee.DepartmentId.HasValue && departments.TryGetValue(employee.DepartmentId.Value, out var name) ? name : string.Empty,
                        UsedVacationDays = usedDays,
                        AnnualVacationLimit = annualLimit,
                        RemainingVacationDays = remaining
                    };
                })
                .OrderBy(r => r.EmployeeName)
                .ToList();
        }

        private static bool MatchesScheduleDay(Schedule schedule, DateTime date)
        {
            if (schedule.StartDate.Date > date.Date || schedule.EndDate.Date < date.Date)
                return false;

            if (schedule.Days == null || schedule.Days.Count == 0)
                return true;

            return schedule.Days.Contains(date.DayOfWeek);
        }

        private List<LateEarlyReportRow> BuildLateRows(List<Employee> employees, MonthlyPunchReportViewModel vm)
        {
            var hasLateInFilter = vm.LateInMinutes.HasValue;
            var hasEarlyOutFilter = vm.EarlyOutMinutes.HasValue;
            var hasOvertimeFilter = vm.OvertimeMinutes.HasValue;

            if (!hasLateInFilter && !hasEarlyOutFilter && !hasOvertimeFilter)
                return new List<LateEarlyReportRow>();

            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var departments = vm.Departments.ToDictionary(d => d.Id, d => d.Name);

            var punches = _repo.GetPunches()
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .Where(p => p.Timestamp >= vm.LateFrom.Date && p.Timestamp < vm.LateTo.Date.AddDays(1))
                .OrderBy(p => p.Timestamp)
                .ToList();

            var schedules = _repo.GetSchedules()
                .Where(s => employeeIds.Contains(s.EmployeeId))
                .Where(s => s.StartDate.Date <= vm.LateTo.Date && s.EndDate.Date >= vm.LateFrom.Date)
                .ToList();

            var rows = new List<LateEarlyReportRow>();

            foreach (var employee in employees)
            {
                var employeePunches = punches.Where(p => p.EmployeeId == employee.Id).ToList();
                var employeeSchedules = schedules.Where(s => s.EmployeeId == employee.Id).ToList();

                for (var date = vm.LateFrom.Date; date <= vm.LateTo.Date; date = date.AddDays(1))
                {
                    var schedule = employeeSchedules.FirstOrDefault(s => MatchesScheduleDay(s, date));
                    if (schedule == null)
                        continue;

                    var isNightShift = schedule.ShiftEnd < schedule.ShiftStart;
                    var shiftStart = date.Date + schedule.ShiftStart;
                    var shiftEnd = isNightShift ? date.Date.AddDays(1) + schedule.ShiftEnd : date.Date + schedule.ShiftEnd;

                    var shiftPunches = employeePunches
                        .Where(p => p.Timestamp >= shiftStart.AddHours(-4) && p.Timestamp <= shiftEnd.AddHours(8))
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    var inPunch = shiftPunches.FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase));
                    var outPunch = shiftPunches.LastOrDefault(p => p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase));

                    var lateMinutes = inPunch != null ? Math.Max((int)Math.Round((inPunch.Timestamp - shiftStart).TotalMinutes), 0) : 0;
                    var earlyMinutes = outPunch != null ? Math.Max((int)Math.Round((shiftEnd - outPunch.Timestamp).TotalMinutes), 0) : 0;
                    var overtimeMinutes = outPunch != null ? Math.Max((int)Math.Round((outPunch.Timestamp - shiftEnd).TotalMinutes), 0) : 0;

                    var matches =
                        (hasLateInFilter && lateMinutes > vm.LateInMinutes!.Value) ||
                        (hasEarlyOutFilter && earlyMinutes > vm.EarlyOutMinutes!.Value) ||
                        (hasOvertimeFilter && overtimeMinutes > vm.OvertimeMinutes!.Value);

                    if (!matches)
                        continue;

                    rows.Add(new LateEarlyReportRow
                    {
                        WorkDate = date,
                        EmployeeName = employee.FullName,
                        DepartmentName = employee.DepartmentId.HasValue && departments.TryGetValue(employee.DepartmentId.Value, out var departmentName) ? departmentName : string.Empty,
                        ScheduleText = $"{schedule.ShiftStart:hh\\:mm} - {schedule.ShiftEnd:hh\\:mm}",
                        PunchIn = inPunch?.Timestamp,
                        PunchOut = outPunch?.Timestamp,
                        LateInMinutes = lateMinutes,
                        EarlyOutMinutes = earlyMinutes,
                        OvertimeMinutes = overtimeMinutes
                    });
                }
            }

            return rows
                .OrderBy(r => r.WorkDate)
                .ThenBy(r => r.EmployeeName)
                .ToList();
        }

        private List<SalaryExportRow> BuildSalaryRows(List<Employee> employees, MonthlyPunchReportViewModel vm)
        {
            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var monthStart = new DateTime(vm.Year, vm.Month, 1);
            var monthEnd = new DateTime(vm.Year, vm.Month, DateTime.DaysInMonth(vm.Year, vm.Month));
            var reportEnd = monthEnd.AddDays(1);

            var schedules = _repo.GetSchedules()
                .Where(s => employeeIds.Contains(s.EmployeeId))
                .Where(s => s.StartDate.Date <= reportEnd.Date && s.EndDate.Date >= monthStart.Date)
                .ToList();

            var templatesById = _repo.GetTemplates().ToDictionary(t => t.Id);
            var vacationTypesById = _repo.GetVacationTypes().ToDictionary(v => v.Id);
            var vacations = _repo.GetVacationsInRange(monthStart, reportEnd)
                .Where(v => v.IsActive && employeeIds.Contains(v.EmployeeId))
                .ToList();

            var punches = _repo.GetPunches()
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .Where(p => p.Timestamp >= monthStart.AddDays(-1) && p.Timestamp < reportEnd.AddDays(1))
                .OrderBy(p => p.Timestamp)
                .ToList();

            var rows = new List<SalaryExportRow>();

            foreach (var employee in employees)
            {
                var employeeSchedules = schedules.Where(s => s.EmployeeId == employee.Id).ToList();
                var employeeVacations = vacations.Where(v => v.EmployeeId == employee.Id).ToList();
                var employeePunches = punches.Where(p => p.EmployeeId == employee.Id).ToList();
                var employeeRows = new List<SalaryExportRow>();

                for (var date = monthStart.Date; date <= monthEnd.Date; date = date.AddDays(1))
                {
                    var daySchedules = employeeSchedules
                        .Where(s => MatchesScheduleDay(s, date))
                        .OrderBy(s => s.ShiftStart)
                        .ToList();

                    var vacation = employeeVacations
                        .FirstOrDefault(v => v.StartDate.Date <= date.Date && v.EndDate.Date >= date.Date);

                    var hasScheduleRow = false;
                    foreach (var schedule in daySchedules)
                    {
                        hasScheduleRow = true;
                        var isNightShift = schedule.ShiftEnd <= schedule.ShiftStart;
                        var shiftStart = date.Date + schedule.ShiftStart;
                        var shiftEnd = isNightShift ? date.Date.AddDays(1) + schedule.ShiftEnd : date.Date + schedule.ShiftEnd;
                        var totalHours = RoundSalaryHours((shiftEnd - shiftStart).TotalHours);
                        var breakHours = RoundSalaryHours(schedule.BreakMinutes / 60.0);
                        var workHours = RoundSalaryHours(Math.Max(totalHours - breakHours, 0));
                        var scheduleName = GetSalaryScheduleName(schedule, templatesById);
                        var lateHours = GetSalaryLateHours(employeePunches, shiftStart, shiftEnd);

                        var row = CreateSalaryBaseRow(employee, date, vacation, vacationTypesById);
                        row.CalendarStart = shiftStart;
                        row.CalendarEnd = shiftEnd;

                        if (!HasSalaryAbsenceValue(row))
                        {
                            row.ScheduleName = scheduleName;
                            row.ScheduledHours = totalHours;
                            row.WorkHours = workHours;
                            row.BreakHours = breakHours;
                            row.CalendarHours = totalHours;
                            row.LateHours = lateHours;

                            if (isNightShift)
                                row.Part3 = scheduleName;
                            else
                                row.Part2 = scheduleName;
                        }

                        employeeRows.Add(row);

                        if (isNightShift && shiftEnd.Date >= monthStart.Date && shiftEnd.Date <= reportEnd.Date)
                        {
                            var endVacation = employeeVacations
                                .FirstOrDefault(v => v.StartDate.Date <= shiftEnd.Date && v.EndDate.Date >= shiftEnd.Date);
                            var endRow = CreateSalaryBaseRow(employee, shiftEnd.Date, endVacation, vacationTypesById);
                            endRow.CalendarStart = shiftStart;
                            endRow.CalendarEnd = shiftEnd;

                            if (!HasSalaryAbsenceValue(endRow))
                            {
                                endRow.ScheduleName = scheduleName;
                                endRow.CalendarHours = totalHours;
                                endRow.Part1 = scheduleName;
                            }

                            employeeRows.Add(endRow);
                        }
                    }

                    if (!hasScheduleRow && vacation != null)
                    {
                        employeeRows.Add(CreateSalaryBaseRow(employee, date, vacation, vacationTypesById));
                    }
                }

                ApplyWeeklySalaryOvertime(employeeRows);
                rows.AddRange(employeeRows);
            }

            return rows
                .OrderBy(r => r.PeriodDate)
                .ThenBy(r => r.EmployeeName)
                .ThenBy(r => r.CalendarStart ?? r.PeriodDate)
                .ToList();
        }

        private static SalaryExportRow CreateSalaryBaseRow(
            Employee employee,
            DateTime date,
            Vacation? vacation,
            Dictionary<int, VacationType> vacationTypesById)
        {
            var row = new SalaryExportRow
            {
                PeriodDate = date.Date,
                EmployeeName = employee.FullName,
                Active = 1
            };

            if (vacation == null || !vacationTypesById.TryGetValue(vacation.VacationTypeId, out var vacationType))
                return row;

            var integrationValue = vacationType.IntegrationValue ?? string.Empty;
            var typeName = vacationType.Name ?? string.Empty;

            if (typeName.Contains("ანაზღაურებადი შვებულება", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("ანაზღაურების გარეშე შვებულება", StringComparison.OrdinalIgnoreCase))
            {
                row.VacationValue = integrationValue;
            }
            else if (typeName.Contains("გაცდენა", StringComparison.OrdinalIgnoreCase))
            {
                row.AbsenceValue = integrationValue;
            }
            else if (typeName.Contains("საავადმყოფო ფურცელი", StringComparison.OrdinalIgnoreCase))
            {
                row.SickLeaveValue = integrationValue;
            }

            return row;
        }

        private static bool HasSalaryAbsenceValue(SalaryExportRow row)
        {
            return !string.IsNullOrWhiteSpace(row.VacationValue) ||
                   !string.IsNullOrWhiteSpace(row.AbsenceValue) ||
                   !string.IsNullOrWhiteSpace(row.SickLeaveValue);
        }

        private static string GetSalaryScheduleName(Schedule schedule, Dictionary<int, ScheduleTemplate> templatesById)
        {
            if (schedule.ScheduleTemplateId.HasValue &&
                templatesById.TryGetValue(schedule.ScheduleTemplateId.Value, out var template) &&
                !string.IsNullOrWhiteSpace(template.Name))
            {
                return template.Name;
            }

            if (!string.IsNullOrWhiteSpace(schedule.ScheduleType))
                return schedule.ScheduleType;

            return $"{schedule.ShiftStart:hh\\:mm} - {schedule.ShiftEnd:hh\\:mm}";
        }

        private static double? GetSalaryLateHours(List<Punch> employeePunches, DateTime shiftStart, DateTime shiftEnd)
        {
            var inPunch = employeePunches
                .Where(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase))
                .Where(p => p.Timestamp >= shiftStart.AddHours(-2) && p.Timestamp <= shiftEnd)
                .OrderBy(p => p.Timestamp)
                .FirstOrDefault();

            if (inPunch == null)
                return null;

            var lateMinutes = Math.Max((inPunch.Timestamp - shiftStart).TotalMinutes, 0);
            return lateMinutes > 60 ? RoundSalaryHours(lateMinutes / 60.0) : null;
        }

        private static void ApplyWeeklySalaryOvertime(List<SalaryExportRow> rows)
        {
            foreach (var weekGroup in rows
                .Where(r => r.WorkHours.HasValue && r.WorkHours.Value > 0)
                .GroupBy(r => GetMonday(r.PeriodDate)))
            {
                var totalHours = weekGroup.Sum(r => r.WorkHours ?? 0);
                var overtime = RoundSalaryHours(Math.Max(totalHours - 40, 0));
                if (overtime <= 0)
                    continue;

                var lastWorkedRow = weekGroup
                    .OrderBy(r => r.PeriodDate)
                    .ThenBy(r => r.CalendarStart ?? r.PeriodDate)
                    .Last();
                lastWorkedRow.OvertimeHours = overtime;
            }
        }

        private static DateTime GetMonday(DateTime date)
        {
            return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        private static double RoundSalaryHours(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
>>>>>>> master
