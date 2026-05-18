using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;
using System;
using System.Linq;
using System.IO;

namespace PunchServerMVC.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly IRepository _repo;
        private readonly IWebHostEnvironment _env;

        public EmployeesController(IRepository repo, IWebHostEnvironment env)
        {
            _repo = repo;
            _env = env;
        }

        private string ProfileImagesFolder => Path.Combine(_env.WebRootPath, "profile_images");
        private string PunchImagesFolder => Path.Combine(_env.WebRootPath, "punch_images");

        private string? GetFallbackPunchImage(Employee employee)
        {
            return _repo.GetAllPunches()
                .Where(p => p.EmployeeId == employee.Id && !string.IsNullOrWhiteSpace(p.ImageUrl))
                .OrderBy(p => p.Timestamp)
                .Select(p => p.ImageUrl)
                .FirstOrDefault(imageUrl => System.IO.File.Exists(Path.Combine(PunchImagesFolder, imageUrl!)));
        }

        private void EnsureAllEmployeeProfileImages()
        {
            foreach (var employee in _repo.GetEmployees().ToList())
            {
                EnsureEmployeeProfileImage(employee);
            }
        }

        private string? EnsureExistingPhotoPath(Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.PhotoUrl))
                return null;

            Directory.CreateDirectory(ProfileImagesFolder);

            var currentProfilePath = Path.Combine(ProfileImagesFolder, employee.PhotoUrl);
            if (System.IO.File.Exists(currentProfilePath))
                return employee.PhotoUrl;

            var oldPunchPath = Path.Combine(PunchImagesFolder, employee.PhotoUrl);
            if (!System.IO.File.Exists(oldPunchPath))
                return null;

            var bytes = System.IO.File.ReadAllBytes(oldPunchPath);
            employee.PhotoUrl = SaveProfileImage(employee, bytes, Path.GetExtension(oldPunchPath));
            _repo.UpdateEmployee(employee);
            return employee.PhotoUrl;
        }

        private string? EnsureEmployeeProfileImage(Employee employee)
        {
            var existingPhoto = EnsureExistingPhotoPath(employee);
            if (!string.IsNullOrWhiteSpace(existingPhoto))
                return existingPhoto;

            var fallbackImage = GetFallbackPunchImage(employee);
            if (string.IsNullOrWhiteSpace(fallbackImage))
                return null;

            var sourcePath = Path.Combine(PunchImagesFolder, fallbackImage);
            if (!System.IO.File.Exists(sourcePath))
                return null;

            var bytes = System.IO.File.ReadAllBytes(sourcePath);
            employee.PhotoUrl = SaveProfileImage(employee, bytes, Path.GetExtension(sourcePath));
            _repo.UpdateEmployee(employee);
            return employee.PhotoUrl;
        }

        private string? ResolveEmployeePhotoUrl(Employee employee)
        {
            return EnsureEmployeeProfileImage(employee);
        }

        private string? ResolveEmployeePhotoPath(Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.PhotoUrl) ||
                !employee.PhotoUrl.StartsWith("employee_", StringComparison.OrdinalIgnoreCase))
                return null;

            var profilePath = Path.Combine(ProfileImagesFolder, employee.PhotoUrl);
            if (System.IO.File.Exists(profilePath))
                return profilePath;

            return null;
        }

        private void DeleteExistingProfileImage(Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.PhotoUrl))
                return;

            var existingPath = Path.Combine(ProfileImagesFolder, employee.PhotoUrl);
            if (System.IO.File.Exists(existingPath))
                System.IO.File.Delete(existingPath);
        }

        private string? SaveProfileImage(Employee employee, byte[] bytes, string extension)
        {
            Directory.CreateDirectory(ProfileImagesFolder);
            DeleteExistingProfileImage(employee);

            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
            var fileName = $"employee_{employee.Id}{safeExtension}";
            var filePath = Path.Combine(ProfileImagesFolder, fileName);
            System.IO.File.WriteAllBytes(filePath, bytes);
            return fileName;
        }

        private PunchLogViewModel BuildEmployeePunchLog(int employeeId, DateTime? from = null, DateTime? to = null, string? punchType = null)
        {
            var now = DateTime.Now;
            var defaultFrom = new DateTime(now.Year, now.Month, 1);
            var defaultTo = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

            var vm = new PunchLogViewModel
            {
                From = from ?? defaultFrom,
                To = to ?? defaultTo,
                PunchType = string.IsNullOrWhiteSpace(punchType) ? null : punchType,
                EmployeeId = employeeId,
                Employees = _repo.GetEmployees().OrderBy(e => e.FullName).ToList()
            };

            var fromDt = vm.From.Date;
            var toExclusive = vm.To.Date.AddDays(1);

            var punches = _repo.GetAllPunches()
                .Where(p => p.EmployeeId == employeeId)
                .Where(p => p.Timestamp >= fromDt && p.Timestamp < toExclusive);

            if (!string.IsNullOrWhiteSpace(vm.PunchType))
                punches = punches.Where(p => p.PunchType.Equals(vm.PunchType, StringComparison.OrdinalIgnoreCase));

            vm.Punches = punches
                .OrderByDescending(p => p.Timestamp)
                .ToList();

            return vm;
        }

        public IActionResult Index(string? fullName, string? personalId, int? organisationId, int? departmentId, bool? isActive)
        {
            var hideProfilePictures = _repo.GetAdministrationSettings().HideProfilePictures;

            var employees = _repo.GetEmployees();
            var hasIsActiveFilter = Request.Query.ContainsKey("isActive");
            var effectiveIsActive = hasIsActiveFilter ? isActive : true;

            if (!string.IsNullOrWhiteSpace(fullName))
                employees = employees.Where(e => e.FullName.Contains(fullName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(personalId))
                employees = employees.Where(e => e.PersonalId.Contains(personalId));

            if (organisationId.HasValue)
                employees = employees.Where(e => e.OrganisationId == organisationId.Value);

            if (departmentId.HasValue)
                employees = employees.Where(e => e.DepartmentId == departmentId.Value);

            if (effectiveIsActive.HasValue)
                employees = employees.Where(e => e.IsActive == effectiveIsActive.Value);

            var vm = new EmployeeFilterViewModel
            {
                FullName = fullName,
                PersonalId = personalId,
                OrganisationId = organisationId,
                DepartmentId = departmentId,
                IsActive = effectiveIsActive,
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments(),
                Employees = employees.ToList()
            };

            ViewBag.HideProfilePictures = hideProfilePictures;
            return View(vm);
        }

        [HttpGet]
        public IActionResult ProfileImage(int id)
        {
            if (_repo.GetAdministrationSettings().HideProfilePictures)
                return NotFound();

            var employee = _repo.GetEmployees().FirstOrDefault(e => e.Id == id);
            if (employee == null)
                return NotFound();

            var imagePath = ResolveEmployeePhotoPath(employee);
            if (string.IsNullOrWhiteSpace(imagePath))
                return NotFound();

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            var contentType = Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return PhysicalFile(imagePath, contentType);
        }

        public IActionResult Create()
        {
            ViewBag.HideProfilePictures = _repo.GetAdministrationSettings().HideProfilePictures;
            var vm = new EmployeeFormViewModel
            {
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EmployeeFormViewModel model, IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.HideProfilePictures = _repo.GetAdministrationSettings().HideProfilePictures;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }

            try
            {
                _repo.AddEmployee(model.Employee);
                if (profileImage != null && profileImage.Length > 0)
                {
                    using var ms = new MemoryStream();
                    profileImage.CopyTo(ms);
                    model.Employee.PhotoUrl = SaveProfileImage(model.Employee, ms.ToArray(), Path.GetExtension(profileImage.FileName));
                    _repo.UpdateEmployee(model.Employee);
                }
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // ✅ Show friendly error message for duplicates
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.HideProfilePictures = _repo.GetAdministrationSettings().HideProfilePictures;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
            catch (Exception ex)
            {
                // Fallback for unexpected issues
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                ViewBag.HideProfilePictures = _repo.GetAdministrationSettings().HideProfilePictures;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                return View(model);
            }
        }

        public IActionResult Edit(int id, DateTime? from, DateTime? to, string? punchType, string? activeTab)
        {
            var employee = _repo.GetEmployees().FirstOrDefault(e => e.Id == id);
            if (employee == null) return NotFound();

            var settings = _repo.GetAdministrationSettings();
            var hideProfilePictures = settings.HideProfilePictures;
            var hidePunchImages = settings.HidePunchImages;
            if (!hideProfilePictures)
                EnsureEmployeeProfileImage(employee);

            ViewBag.ActiveTab = string.Equals(activeTab, "punchlog", StringComparison.OrdinalIgnoreCase)
                ? "punchlog"
                : "details";
            ViewBag.HideProfilePictures = hideProfilePictures;
            ViewBag.HidePunchImages = hidePunchImages;

            var vm = new EmployeeFormViewModel
            {
                Employee = employee,
                Organisations = _repo.GetOrganisations(),
                Departments = _repo.GetDepartments(),
                PunchLog = BuildEmployeePunchLog(id, from, to, punchType),
                EffectivePhotoUrl = hideProfilePictures ? null : ResolveEmployeePhotoUrl(employee)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EmployeeFormViewModel model, IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
            {
                var settings = _repo.GetAdministrationSettings();
                ViewBag.HideProfilePictures = settings.HideProfilePictures;
                ViewBag.HidePunchImages = settings.HidePunchImages;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                model.PunchLog = BuildEmployeePunchLog(model.Employee.Id);
                model.EffectivePhotoUrl = settings.HideProfilePictures ? null : ResolveEmployeePhotoUrl(model.Employee);
                return View(model);
            }

            try
            {
                if (profileImage != null && profileImage.Length > 0)
                {
                    using var ms = new MemoryStream();
                    profileImage.CopyTo(ms);
                    model.Employee.PhotoUrl = SaveProfileImage(model.Employee, ms.ToArray(), Path.GetExtension(profileImage.FileName));
                }

                _repo.UpdateEmployee(model.Employee);
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // ✅ Friendly message when updating duplicates
                ModelState.AddModelError(string.Empty, ex.Message);
                var settings = _repo.GetAdministrationSettings();
                ViewBag.HideProfilePictures = settings.HideProfilePictures;
                ViewBag.HidePunchImages = settings.HidePunchImages;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                model.PunchLog = BuildEmployeePunchLog(model.Employee.Id);
                model.EffectivePhotoUrl = settings.HideProfilePictures ? null : ResolveEmployeePhotoUrl(model.Employee);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                var settings = _repo.GetAdministrationSettings();
                ViewBag.HideProfilePictures = settings.HideProfilePictures;
                ViewBag.HidePunchImages = settings.HidePunchImages;
                model.Organisations = _repo.GetOrganisations();
                model.Departments = _repo.GetDepartments();
                model.PunchLog = BuildEmployeePunchLog(model.Employee.Id);
                model.EffectivePhotoUrl = settings.HideProfilePictures ? null : ResolveEmployeePhotoUrl(model.Employee);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetProfileImageFromPunch(int employeeId, int punchId, DateTime? from, DateTime? to, string? punchType, string? activeTab)
        {
            var employee = _repo.GetEmployees().FirstOrDefault(e => e.Id == employeeId);
            var punch = _repo.GetPunches().FirstOrDefault(p => p.Id == punchId && p.EmployeeId == employeeId);

            if (employee == null || punch == null || string.IsNullOrWhiteSpace(punch.ImageUrl))
            {
                TempData["Error"] = "Punch image not found.";
                return RedirectToAction(nameof(Edit), new { id = employeeId, from, to, punchType, activeTab });
            }

            var sourcePath = Path.Combine(PunchImagesFolder, punch.ImageUrl);
            if (!System.IO.File.Exists(sourcePath))
            {
                TempData["Error"] = "Punch image file not found.";
                return RedirectToAction(nameof(Edit), new { id = employeeId, from, to, punchType, activeTab });
            }

            var bytes = System.IO.File.ReadAllBytes(sourcePath);
            employee.PhotoUrl = SaveProfileImage(employee, bytes, Path.GetExtension(sourcePath));
            _repo.UpdateEmployee(employee);

            TempData["Success"] = "Profile picture updated from punch image.";
            return RedirectToAction(nameof(Edit), new { id = employeeId, from, to, punchType, activeTab });
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
