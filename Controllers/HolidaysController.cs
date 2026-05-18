using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Controllers
{
    public class HolidaysController : Controller
    {
        private readonly IRepository _repo;

        public HolidaysController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(int? year)
        {
            var selectedYear = year ?? DateTime.Today.Year;
            var holidays = _repo.GetHolidays()
                .Where(h => h.Date.Year == selectedYear)
                .OrderBy(h => h.Date)
                .ToList();

            ViewBag.SelectedYear = selectedYear;
            ViewBag.AvailableYears = _repo.GetHolidays()
                .Select(h => h.Date.Year)
                .Append(selectedYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            return View(holidays);
        }

        public IActionResult Create()
        {
            return View(new Holiday
            {
                Date = DateTime.Today,
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Holiday model)
        {
            model.Date = model.Date.Date;

            if (_repo.GetHolidays().Any(h => h.Date.Date == model.Date.Date))
                ModelState.AddModelError(nameof(Holiday.Date), "A holiday already exists for this date.");

            if (!ModelState.IsValid)
                return View(model);

            _repo.AddHoliday(model);
            TempData["Success"] = "Holiday added successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "holidays", year = model.Date.Year });
        }

        public IActionResult Edit(int id)
        {
            var holiday = _repo.GetHolidayById(id);
            return holiday == null ? NotFound() : View(holiday);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Holiday model)
        {
            model.Date = model.Date.Date;

            if (_repo.GetHolidays().Any(h => h.Id != model.Id && h.Date.Date == model.Date.Date))
                ModelState.AddModelError(nameof(Holiday.Date), "A holiday already exists for this date.");

            if (!ModelState.IsValid)
                return View(model);

            model.UpdatedAt = DateTime.UtcNow;
            _repo.UpdateHoliday(model);
            TempData["Success"] = "Holiday updated successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "holidays", year = model.Date.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var holiday = _repo.GetHolidayById(id);
            _repo.DeleteHoliday(id);
            TempData["Success"] = "Holiday deleted successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "holidays", year = holiday?.Date.Year ?? DateTime.Today.Year });
        }

        public IActionResult Copy(int year)
        {
            var targetYear = year + 1;
            var existingTargetDates = _repo.GetHolidays()
                .Where(h => h.Date.Year == targetYear)
                .Select(h => h.Date.Date)
                .ToHashSet();

            var model = new HolidayCopyViewModel
            {
                SourceYear = year,
                TargetYear = targetYear,
                Holidays = _repo.GetHolidays()
                    .Where(h => h.Date.Year == year)
                    .OrderBy(h => h.Date)
                    .Select(h =>
                    {
                        var targetDate = h.Date.Date.AddYears(1);
                        return new HolidayCopyRowViewModel
                        {
                            Include = !existingTargetDates.Contains(targetDate),
                            SourceDate = h.Date.Date,
                            Date = targetDate,
                            Name = h.Name,
                            Description = h.Description,
                            IsActive = h.IsActive,
                            AlreadyExists = existingTargetDates.Contains(targetDate)
                        };
                    })
                    .ToList()
            };

            if (!model.Holidays.Any())
            {
                TempData["Error"] = $"No holidays found for {year}.";
                return RedirectToAction("Index", "Settings", new { activeTab = "holidays", year });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CopyConfirm(HolidayCopyViewModel model)
        {
            var includedRows = model.Holidays
                .Where(h => h.Include)
                .ToList();

            if (!includedRows.Any())
            {
                TempData["Error"] = "No holidays selected to copy.";
                return View("Copy", model);
            }

            for (var i = 0; i < model.Holidays.Count; i++)
            {
                if (!model.Holidays[i].Include)
                    continue;

                if (string.IsNullOrWhiteSpace(model.Holidays[i].Name))
                    ModelState.AddModelError($"Holidays[{i}].Name", "Name is required.");
            }

            if (!ModelState.IsValid)
                return View("Copy", model);

            var existingDates = _repo.GetHolidays()
                .Select(h => h.Date.Date)
                .ToHashSet();

            var added = 0;
            var skipped = 0;

            foreach (var row in includedRows.OrderBy(h => h.Date))
            {
                var date = row.Date.Date;
                if (existingDates.Contains(date))
                {
                    skipped++;
                    continue;
                }

                _repo.AddHoliday(new Holiday
                {
                    Date = date,
                    Name = row.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                    IsActive = row.IsActive,
                    CreatedAt = DateTime.UtcNow
                });

                existingDates.Add(date);
                added++;
            }

            TempData["Success"] = skipped > 0
                ? $"Copied {added} holidays. Skipped {skipped} duplicate dates."
                : $"Copied {added} holidays.";

            var redirectYear = includedRows.FirstOrDefault()?.Date.Year ?? model.TargetYear;
            return RedirectToAction("Index", "Settings", new { activeTab = "holidays", year = redirectYear });
        }
    }
}
