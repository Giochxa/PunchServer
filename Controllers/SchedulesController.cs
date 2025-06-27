using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PunchServerMVC.Controllers
{
    public class SchedulesController : Controller
    {
        private readonly IRepository _repo;

        public SchedulesController(IRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var schedules = _repo.GetSchedules().ToList();
            var employees = _repo.GetEmployees().ToList();

            var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
            ViewBag.EmployeeNames = employeeNames;

            return View(schedules);
        }

        public IActionResult Create()
        {
            ViewBag.Templates = _repo.GetTemplates().ToList();
            ViewBag.Employees = _repo.GetEmployees().ToList();
            return View(new Schedule());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Schedule schedule)
        {
            schedule.Days = Request.Form["Days"]
                .Select(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            if (schedule.StartDate > schedule.EndDate)
            {
                ModelState.AddModelError(string.Empty, "Start Date must be before or equal to End Date.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Employees = _repo.GetEmployees().ToList();
                ViewBag.Templates = _repo.GetTemplates().ToList();
                return View(schedule);
            }

            _repo.AddSchedule(schedule);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null) return NotFound();

            ViewBag.Templates = _repo.GetTemplates().ToList();
            ViewBag.Employees = _repo.GetEmployees().ToList();
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Schedule model, List<DayOfWeek> Days)
        {
            if (model.StartDate > model.EndDate)
            {
                ModelState.AddModelError(string.Empty, "Start Date must be before or equal to End Date.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Employees = _repo.GetEmployees().ToList();
                ViewBag.Templates = _repo.GetTemplates().ToList();
                return View(model);
            }

            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null)
                return NotFound();

            schedule.EmployeeId = model.EmployeeId;
            schedule.ScheduleTemplateId = model.ScheduleTemplateId;
            schedule.ShiftStart = model.ShiftStart;
            schedule.ShiftEnd = model.ShiftEnd;
            schedule.BreakMinutes = model.BreakMinutes;
            schedule.ScheduleType = model.ScheduleType;
            schedule.StartDate = model.StartDate;
            schedule.EndDate = model.EndDate;
            schedule.Days = Days;

            _repo.UpdateSchedule(schedule);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var schedule = _repo.GetSchedules().FirstOrDefault(s => s.Id == id);
            if (schedule == null) return NotFound();

            _repo.DeleteSchedule(schedule.Id);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Grid()
        {
            ViewBag.Schedules = _repo.GetSchedules().ToList();
            return View(_repo.GetEmployees().ToList());
        }

        public IActionResult WeeklyGrid()
        {
            ViewBag.Schedules = _repo.GetSchedules().ToList();
            return View(_repo.GetEmployees().ToList());
        }
    }
}
