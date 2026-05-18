using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IRepository _repo;

        public SettingsController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(string? activeTab, int? year)
        {
            var selectedYear = year ?? DateTime.Today.Year;
            var lastAutoPunchOutRun = _repo.GetAutoPunchOutRunLogs(1).FirstOrDefault();

            var vm = new SettingsIndexViewModel
            {
                ActiveTab = string.IsNullOrWhiteSpace(activeTab) ? "holidays" : activeTab,
                SelectedYear = selectedYear,
                AvailableHolidayYears = _repo.GetHolidays()
                    .Select(h => h.Date.Year)
                    .Append(selectedYear)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList(),
                Holidays = _repo.GetHolidays()
                    .Where(h => h.Date.Year == selectedYear)
                    .OrderBy(h => h.Date)
                    .ToList(),
                VacationTypes = _repo.GetVacationTypes()
                    .OrderBy(v => v.Name)
                    .ToList(),
                Organisations = _repo.GetOrganisations()
                    .OrderBy(o => o.Name)
                    .ToList(),
                Departments = _repo.GetDepartments()
                    .OrderBy(d => d.Name)
                    .ToList(),
                Templates = _repo.GetTemplates()
                    .OrderBy(t => t.Name)
                    .ToList(),
                AdministrationSettings = _repo.GetAdministrationSettings(),
                LastAutoPunchOutRun = lastAutoPunchOutRun
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveAdministration(SettingsIndexViewModel model)
        {
            var settings = _repo.GetAdministrationSettings();
            settings.AutoPunchOutEnabled = model.AdministrationSettings.AutoPunchOutEnabled;
            settings.AutoPunchOutTimeCheck = model.AdministrationSettings.AutoPunchOutEnabled
                ? model.AdministrationSettings.AutoPunchOutTimeCheck
                : null;
            settings.DailyBackupTimeCheck = model.AdministrationSettings.DailyBackupTimeCheck;
            settings.CalendarPunchGraceMinutes = model.AdministrationSettings.CalendarPunchGraceMinutes > 0
                ? model.AdministrationSettings.CalendarPunchGraceMinutes
                : 15;
            settings.MissingInLookbackMinutes = model.AdministrationSettings.MissingInLookbackMinutes > 0
                ? model.AdministrationSettings.MissingInLookbackMinutes
                : 120;
            settings.HidePunchImages = model.AdministrationSettings.HidePunchImages;
            settings.HideProfilePictures = model.AdministrationSettings.HideProfilePictures;

            _repo.SaveAdministrationSettings(settings);
            TempData["Success"] = "Administration settings saved successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "administration" });
        }

        public IActionResult AutoPunchOutRuns(int? employeeId, DateTime? fromDate, DateTime? toDate)
        {
            var today = DateTime.Today;
            var defaultFrom = today.AddDays(-6).Date;
            var defaultTo = today.Date;

            var filterFrom = (fromDate ?? defaultFrom).Date;
            var filterTo = (toDate ?? defaultTo).Date;

            var runs = _repo.GetAutoPunchOutRunLogs(200)
                .Where(r => r.StartedAt.Date >= filterFrom && r.StartedAt.Date <= filterTo)
                .Where(r => !employeeId.HasValue || r.AddedPunches.Any(p => p.EmployeeId == employeeId.Value))
                .ToList();

            ViewBag.AutoPunchOutEmployees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList();
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.FilterFrom = filterFrom;
            ViewBag.FilterTo = filterTo;

            return View(runs);
        }
    }
}
