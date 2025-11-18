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
            int? month)
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
                Month = month ?? DateTime.Now.Month
            };

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

            // Get all punches for this month once
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
                    continue; // no punches for this employee in this month

                var row = new MonthlyPunchRow
                {
                    OrganisationName = emp.OrganisationId.HasValue && orgLookup.TryGetValue(emp.OrganisationId.Value, out var oname)
                        ? oname
                        : string.Empty,
                    DepartmentName = emp.DepartmentId.HasValue && deptLookup.TryGetValue(emp.DepartmentId.Value, out var dname)
                        ? dname
                        : string.Empty,
                    EmployeeName = emp.FullName,
                    PersonalId = emp.PersonalId
                };

                for (int day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(vm.Year, vm.Month, day);

                    var dayPunches = empPunches
                        .Where(p => p.Timestamp.Date == date.Date)
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    DateTime? punchIn = null;
                    DateTime? punchOut = null;

                    if (dayPunches.Any())
                    {
                        // Prefer first "In" and last "Out" if PunchType is set
                        var firstIn = dayPunches
                            .FirstOrDefault(p => p.PunchType.Equals("In", StringComparison.OrdinalIgnoreCase));
                        var lastOut = dayPunches
                            .LastOrDefault(p => p.PunchType.Equals("Out", StringComparison.OrdinalIgnoreCase));

                        punchIn = (firstIn ?? dayPunches.First()).Timestamp;
                        punchOut = (lastOut ?? dayPunches.Last()).Timestamp;
                    }

                    double? diff = null;
                    if (punchIn.HasValue && punchOut.HasValue && punchOut.Value >= punchIn.Value)
                        diff = (punchOut.Value - punchIn.Value).TotalHours;

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
