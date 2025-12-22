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
