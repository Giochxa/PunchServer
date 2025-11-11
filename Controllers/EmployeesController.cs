using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;
using System;
using System.Linq;

namespace PunchServerMVC.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly IRepository _repo;

        public EmployeesController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(string? fullName, string? personalId, int? organisationId, int? departmentId, bool? isActive)
        {
            var employees = _repo.GetEmployees();

            if (!string.IsNullOrWhiteSpace(fullName))
                employees = employees.Where(e => e.FullName.Contains(fullName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(personalId))
                employees = employees.Where(e => e.PersonalId.Contains(personalId));

            if (organisationId.HasValue)
                employees = employees.Where(e => e.OrganisationId == organisationId.Value);

            if (departmentId.HasValue)
                employees = employees.Where(e => e.DepartmentId == departmentId.Value);

            if (isActive.HasValue)
                employees = employees.Where(e => e.IsActive == isActive.Value);

            var vm = new EmployeeFilterViewModel
            {
                FullName = fullName,
                PersonalId = personalId,
                OrganisationId = organisationId,
                DepartmentId = departmentId,
                IsActive = isActive,
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments(),
                Employees = employees.ToList()
            };

            return View(vm);
        }

        public IActionResult Create()
        {
            var vm = new EmployeeFormViewModel
            {
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EmployeeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }

            try
            {
                _repo.AddEmployee(model.Employee);
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // ✅ Show friendly error message for duplicates
                ModelState.AddModelError(string.Empty, ex.Message);
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
            catch (Exception ex)
            {
                // Fallback for unexpected issues
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
        }

        public IActionResult Edit(int id)
        {
            var employee = _repo.GetEmployees().FirstOrDefault(e => e.Id == id);
            if (employee == null) return NotFound();

            var vm = new EmployeeFormViewModel
            {
                Employee = employee,
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EmployeeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }

            try
            {
                _repo.UpdateEmployee(model.Employee);
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // ✅ Friendly message when updating duplicates
                ModelState.AddModelError(string.Empty, ex.Message);
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
        }

        public IActionResult Delete(int id)
        {
            var emp = _repo.GetEmployees().FirstOrDefault(e => e.Id == id);
            if (emp == null) return NotFound();
            return View(emp);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.DeleteEmployee(id);
            return RedirectToAction(nameof(Index));
        }

        // ✅ API endpoint for kiosk sync
        [HttpGet("api/employees")]
        public IActionResult GetEmployees()
        {
            var employees = _repo.GetEmployees()
                .Select(e => new
                {
                    e.Id,
                    e.FullName,
                    e.UniqueId,
                    e.PersonalId,
                    e.IsActive
                })
                .ToList();

            return Ok(employees);
        }
    }
}
