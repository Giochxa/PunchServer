using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using System.Collections.Generic;

namespace PunchServerMVC.Controllers
{
    public class OrganisationController : Controller
    {
        private readonly IRepository _repo;

        public OrganisationController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var organisations = _repo.GetOrganisations();
            return View(organisations);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Organisation model)
        {
            if (!ModelState.IsValid) return View(model);

            _repo.AddOrganisation(model);
            TempData["Success"] = "Organisation added successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "organisations" });
        }

        public IActionResult Edit(int id)
        {
            var item = _repo.GetOrganisationById(id);
            return item == null ? NotFound() : View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Organisation model)
        {
            if (!ModelState.IsValid) return View(model);

            _repo.UpdateOrganisation(model);
            TempData["Success"] = "Organisation updated successfully.";
            return RedirectToAction("Index", "Settings", new { activeTab = "organisations" });
        }

        [HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Delete(int id)
{
    _repo.DeleteOrganisation(id);
    TempData["Success"] = "Organisation deleted successfully.";
    return RedirectToAction("Index", "Settings", new { activeTab = "organisations" });
}


        [HttpGet("api/organisations")]
        public IActionResult GetOrganisations()
        {
            var list = _repo.GetOrganisations();
            return Ok(list);
        }
    }
}
