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
            return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var item = _repo.GetOrganisationById(id);
            return item == null ? NotFound() : View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.DeleteOrganisation(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("api/organisations")]
        public IActionResult GetOrganisations()
        {
            var list = _repo.GetOrganisations();
            return Ok(list);
        }
    }
}
