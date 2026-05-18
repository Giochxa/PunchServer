using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Controllers
{
    public class VacationsController : Controller
    {
        private readonly IRepository _repo;

        public VacationsController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(int? employeeId, int? departmentId, DateTime? fromDate, DateTime? toDate, string? activeTab = null)
        {
            var employees = _repo.GetEmployees().ToList();
            var departments = _repo.GetDepartments().OrderBy(d => d.Name).ToList();
            var vacationTypes = _repo.GetVacationTypes().OrderBy(v => v.Name).ToList();
            var today = DateTime.Today;
            var filterFrom = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var filterTo = (toDate ?? new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))).Date;

            var vacations = _repo.GetVacations()
                .Where(v => v.StartDate.Date <= filterTo && v.EndDate.Date >= filterFrom)
                .Where(v => !employeeId.HasValue || v.EmployeeId == employeeId.Value)
                .Where(v =>
                    !departmentId.HasValue ||
                    employees.FirstOrDefault(e => e.Id == v.EmployeeId)?.DepartmentId == departmentId.Value)
                .OrderByDescending(v => v.StartDate)
                .ThenByDescending(v => v.EndDate)
                .Select(v => new VacationListItemViewModel
                {
                    Id = v.Id,
                    EmployeeId = v.EmployeeId,
                    EmployeeName = employees.FirstOrDefault(e => e.Id == v.EmployeeId)?.FullName ?? "Unknown",
                    DepartmentId = employees.FirstOrDefault(e => e.Id == v.EmployeeId)?.DepartmentId,
                    DepartmentName = departments.FirstOrDefault(d => d.Id == employees.FirstOrDefault(e => e.Id == v.EmployeeId)?.DepartmentId)?.Name ?? "",
                    VacationTypeId = v.VacationTypeId,
                    VacationTypeName = vacationTypes.FirstOrDefault(t => t.Id == v.VacationTypeId)?.Name ?? "Unknown",
                    VacationTypeAbbreviation = vacationTypes.FirstOrDefault(t => t.Id == v.VacationTypeId)?.Abbreviation ?? "",
                    StartDate = v.StartDate.Date,
                    EndDate = v.EndDate.Date,
                    Notes = v.Notes,
                    IsActive = v.IsActive
                })
                .ToList();

            var vm = new VacationIndexViewModel
            {
                Employees = employees,
                Departments = departments,
                VacationTypes = vacationTypes,
                Vacations = vacations,
                SelectedEmployeeId = employeeId,
                SelectedDepartmentId = departmentId,
                FilterFrom = filterFrom,
                FilterTo = filterTo,
                ActiveTab = "vacations"
            };

            return View(vm);
        }

        public IActionResult CreateType()
        {
            return View(new VacationType { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateType(VacationType model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();
            model.Abbreviation = (model.Abbreviation ?? string.Empty).Trim().ToUpperInvariant();
            model.IntegrationValue = string.IsNullOrWhiteSpace(model.IntegrationValue)
                ? null
                : model.IntegrationValue.Trim();

            if (_repo.GetVacationTypes().Any(v => v.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(VacationType.Name), "A vacation type with this name already exists.");

            if (_repo.GetVacationTypes().Any(v => v.Abbreviation.Equals(model.Abbreviation, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(VacationType.Abbreviation), "A vacation type with this abbreviation already exists.");

            if (!ModelState.IsValid)
                return View(model);

            _repo.AddVacationType(model);
            TempData["Success"] = "Vacation type added successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "vacationTypes" });
        }

        public IActionResult EditType(int id)
        {
            var type = _repo.GetVacationTypeById(id);
            return type == null ? NotFound() : View(type);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditType(VacationType model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();
            model.Abbreviation = (model.Abbreviation ?? string.Empty).Trim().ToUpperInvariant();
            model.IntegrationValue = string.IsNullOrWhiteSpace(model.IntegrationValue)
                ? null
                : model.IntegrationValue.Trim();

            if (_repo.GetVacationTypes().Any(v => v.Id != model.Id && v.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(VacationType.Name), "A vacation type with this name already exists.");

            if (_repo.GetVacationTypes().Any(v => v.Id != model.Id && v.Abbreviation.Equals(model.Abbreviation, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(VacationType.Abbreviation), "A vacation type with this abbreviation already exists.");

            if (!ModelState.IsValid)
                return View(model);

            model.UpdatedAt = DateTime.UtcNow;
            _repo.UpdateVacationType(model);
            TempData["Success"] = "Vacation type updated successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "vacationTypes" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteType(int id)
        {
            var hasVacations = _repo.GetVacations().Any(v => v.VacationTypeId == id);
            if (hasVacations)
            {
                TempData["Error"] = "Cannot delete a vacation type that is already used by vacations.";
                return RedirectToAction("Index", "Settings", new { activeTab = "vacationTypes" });
            }

            _repo.DeleteVacationType(id);
            TempData["Success"] = "Vacation type deleted successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "vacationTypes" });
        }

        public IActionResult Create()
        {
            return View(new VacationFormViewModel
            {
                Vacation = new Vacation
                {
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today,
                    IsActive = true
                },
                Employees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList(),
                VacationTypes = _repo.GetVacationTypes().Where(v => v.IsActive).OrderBy(v => v.Name).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(VacationFormViewModel model)
        {
            NormalizeVacation(model.Vacation);
            ValidateVacation(model.Vacation);

            if (!ModelState.IsValid)
            {
                model.Employees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList();
                model.VacationTypes = _repo.GetVacationTypes().Where(v => v.IsActive).OrderBy(v => v.Name).ToList();
                return View(model);
            }

            _repo.AddVacation(model.Vacation);
            TempData["Success"] = "Vacation added successfully.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var vacation = _repo.GetVacationById(id);
            if (vacation == null) return NotFound();

            return View(new VacationFormViewModel
            {
                Vacation = vacation,
                Employees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList(),
                VacationTypes = _repo.GetVacationTypes().Where(v => v.IsActive || v.Id == vacation.VacationTypeId).OrderBy(v => v.Name).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(VacationFormViewModel model)
        {
            NormalizeVacation(model.Vacation);
            ValidateVacation(model.Vacation, model.Vacation.Id);

            if (!ModelState.IsValid)
            {
                model.Employees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList();
                model.VacationTypes = _repo.GetVacationTypes().Where(v => v.IsActive || v.Id == model.Vacation.VacationTypeId).OrderBy(v => v.Name).ToList();
                return View(model);
            }

            model.Vacation.UpdatedAt = DateTime.UtcNow;
            _repo.UpdateVacation(model.Vacation);
            TempData["Success"] = "Vacation updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            _repo.DeleteVacation(id);
            TempData["Success"] = "Vacation deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private void NormalizeVacation(Vacation vacation)
        {
            vacation.StartDate = vacation.StartDate.Date;
            vacation.EndDate = vacation.EndDate.Date;
            vacation.Notes = string.IsNullOrWhiteSpace(vacation.Notes) ? null : vacation.Notes.Trim();
        }

        private void ValidateVacation(Vacation vacation, int? vacationId = null)
        {
            if (vacation.EndDate.Date < vacation.StartDate.Date)
                ModelState.AddModelError("Vacation.EndDate", "End date must be on or after start date.");

            if (!_repo.GetEmployees().Any(e => e.Id == vacation.EmployeeId))
                ModelState.AddModelError("Vacation.EmployeeId", "Selected employee was not found.");

            if (!_repo.GetVacationTypes().Any(v => v.Id == vacation.VacationTypeId))
                ModelState.AddModelError("Vacation.VacationTypeId", "Selected vacation type was not found.");

            var overlaps = _repo.GetVacations()
                .Any(v => v.Id != (vacationId ?? 0) &&
                          v.EmployeeId == vacation.EmployeeId &&
                          v.IsActive &&
                          v.StartDate.Date <= vacation.EndDate.Date &&
                          v.EndDate.Date >= vacation.StartDate.Date);

            if (overlaps)
                ModelState.AddModelError("Vacation.StartDate", "This employee already has a vacation overlapping this period.");
        }
    }
}
