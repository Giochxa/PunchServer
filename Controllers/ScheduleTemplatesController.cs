using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PunchServerMVC.Data;
using PunchServerMVC.Models;

namespace PunchServerMVC.Controllers
{
    public class ScheduleTemplatesController : Controller
    {
        private readonly IRepository _repo;

        public ScheduleTemplatesController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var templates = _repo.GetTemplates().ToList();
            return View(templates);
        }

        public IActionResult Create()
        {
            return View(new ScheduleTemplate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ScheduleTemplate template, List<DayOfWeek> Days)
        {
            if (!ModelState.IsValid)
                return View(template);

            template.Days = Days;
            _repo.AddTemplate(template);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var template = _repo.GetTemplates().FirstOrDefault(t => t.Id == id);
            if (template == null) return NotFound();
            return View(template);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ScheduleTemplate template, List<DayOfWeek> Days)
        {
            if (!ModelState.IsValid) return View(template);

            var existing = _repo.GetTemplates().FirstOrDefault(t => t.Id == id);
            if (existing == null) return NotFound();

            existing.Name = template.Name;
            existing.ShiftStart = template.ShiftStart;
            existing.ShiftEnd = template.ShiftEnd;
            existing.Type = template.Type;
            existing.BreakMinutes = template.BreakMinutes;
            existing.Days = Days;

            _repo.UpdateTemplate(existing);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var template = _repo.GetTemplates().FirstOrDefault(t => t.Id == id);
            if (template == null) return NotFound();
            return View(template);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.DeleteTemplate(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
