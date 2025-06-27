using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PunchServer.Models;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using System;
using System.Globalization;
using PunchServerMVC.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;


namespace PunchServer.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRepository _repo;

    public HomeController(ILogger<HomeController> logger, IRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public IActionResult Punch(int employeeId, string punchType)
    {
        _repo.AddPunch(new Punch
        {
            EmployeeId = employeeId,
            PunchType = punchType,
            Timestamp = DateTime.UtcNow
        });

        ViewBag.Message = "Punch recorded!";
        return View("Index");
    }

    public IActionResult PunchLog()
    {
        var punches = _repo.GetAllPunches();
        return View(punches);
    }

public IActionResult Calendar(int? employeeId, int? organisationId, int? departmentId, int month = 0, int year = 0)
{
    var now = DateTime.Now;
    if (month == 0) month = now.Month;
    if (year == 0) year = now.Year;

    var employees = _repo.GetEmployees()
        .Where(e => (!employeeId.HasValue || e.Id == employeeId)
                 && (!organisationId.HasValue || e.OrganisationId == organisationId)
                 && (!departmentId.HasValue || e.DepartmentId == departmentId))
        .ToList();

    var calendarEntries = _repo.GetCalendarEntriesForMonth(year, month, employeeId, organisationId, departmentId);

    var viewModel = new CalendarViewModel
    {
        Year = year,
        Month = month,
        SelectedEmployeeId = employeeId,
        SelectedOrganisationId = organisationId,
        SelectedDepartmentId = departmentId,
        Employees = _repo.GetEmployees().ToList(),
        Organisations = _repo.GetOrganisations().ToList(),
        Departments = organisationId.HasValue
            ? _repo.GetDepartments().Where(d => d.OrganisationId == organisationId.Value).ToList()
            : _repo.GetDepartments().ToList(),
        CalendarEntries = calendarEntries
    };

    return View(viewModel);
}

public IActionResult WorkSheet(int year, int month)
{
    var employees = _repo.GetEmployees().ToList();
    var punches = _repo.GetPunches()
        .Where(p => p.Timestamp.Year == year && p.Timestamp.Month == month)
        .ToList();

    var daysInMonth = DateTime.DaysInMonth(year, month);

    var model = new WorkSheetViewModel
    {
        Year = year,
        Month = month,
        EmployeeSheets = new List<EmployeeWorkSheet>()
    };

    foreach (var emp in employees)
    {
        var empPunches = punches.Where(p => p.EmployeeId == emp.Id).ToList();

        var entries = new List<WorkSheetDayEntry>();
        double totalHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var dailyPunches = empPunches.Where(p => p.Timestamp.Date == date).ToList();
            string status;

            if (dailyPunches.Any())
            {
                status = "✔";
                totalHours += 8; // Placeholder — replace with actual punch difference logic
            }
            else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                status = ""; // weekend
            }
            else
            {
                status = "x";
            }

            entries.Add(new WorkSheetDayEntry { Day = day, Status = status });
        }

        model.EmployeeSheets.Add(new EmployeeWorkSheet
        {
            FullName = emp.FullName,
            Position = emp.Position ?? "", // add Position to Employee model if needed
            TabNumber = emp.PersonalId,
            DailyEntries = entries,
            TotalWorkedHours = totalHours,
            OvertimeHours = 0,
            NightHours = 0,
            WeekendWorkedHours = 0
        });
    }

    return View(model);
}


}
