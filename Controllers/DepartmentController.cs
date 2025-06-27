using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;
using System.Linq;

namespace PunchServerMVC.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly IRepository _repo;

        public DepartmentController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var departments = _repo.GetDepartments();
            return View(departments);
        }

        public IActionResult Create()
        {
            var vm = new DepartmentFormViewModel
            {
                Organisations = _repo.GetOrganisations()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DepartmentFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Organisations = _repo.GetOrganisations(); // Reload list
                return View(model);
            }

            _repo.AddDepartment(model.Department);
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Edit(int id)
        {
            var department = _repo.GetDepartmentById(id);
            if (department == null) return NotFound();

            var vm = new DepartmentFormViewModel
            {
                Department = department,
                Organisations = _repo.GetOrganisations()
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(DepartmentFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Organisations = _repo.GetOrganisations(); // reload dropdown
                return View(model);
            }

            _repo.UpdateDepartment(model.Department);
            return RedirectToAction(nameof(Index));
        }


        public IActionResult Delete(int id)
        {
            var item = _repo.GetDepartmentById(id);
            return item == null ? NotFound() : View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.DeleteDepartment(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("api/departments")]
        public IActionResult GetDepartments()
        {
            var list = _repo.GetDepartments();
            return Ok(list);
        }
        
        [HttpGet("api/departments/by-organisation/{organisationId}")]
public IActionResult GetByOrganisation(int organisationId)
{
    var departments = _repo
        .GetDepartments()
        .Where(d => d.OrganisationId == organisationId)
        .Select(d => new { d.Id, d.Name })
        .ToList();

    return Json(departments);
}

    }
}
