using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PunchServerMVC.Data;
using PunchServerMVC.Models;

namespace PunchServerMVC.Services
{
    public class ProfilePictureAssignmentWorker : BackgroundService
    {
        private readonly ILogger<ProfilePictureAssignmentWorker> _logger;
        private readonly IRepository _repo;
        private readonly IWebHostEnvironment _env;

        public ProfilePictureAssignmentWorker(
            ILogger<ProfilePictureAssignmentWorker> logger,
            IRepository repo,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _repo = repo;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    AssignMissingProfilePictures();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Profile picture assignment worker failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private void AssignMissingProfilePictures()
        {
            var profileImagesFolder = Path.Combine(_env.WebRootPath, "profile_images");
            var punchImagesFolder = Path.Combine(_env.WebRootPath, "punch_images");
            Directory.CreateDirectory(profileImagesFolder);

            var punchesWithImages = _repo.GetAllPunches()
                .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
                .OrderBy(p => p.Timestamp)
                .GroupBy(p => p.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.FirstOrDefault(p => File.Exists(Path.Combine(punchImagesFolder, Path.GetFileName(p.ImageUrl)))));

            foreach (var employee in _repo.GetEmployees().ToList())
            {
                if (HasValidProfilePicture(employee, profileImagesFolder))
                    continue;

                if (!punchesWithImages.TryGetValue(employee.Id, out var punch) ||
                    punch == null ||
                    string.IsNullOrWhiteSpace(punch.ImageUrl))
                {
                    continue;
                }

                var sourceFileName = Path.GetFileName(punch.ImageUrl);
                var sourcePath = Path.Combine(punchImagesFolder, sourceFileName);
                if (!File.Exists(sourcePath))
                    continue;

                var extension = Path.GetExtension(sourceFileName);
                if (string.IsNullOrWhiteSpace(extension))
                    extension = ".jpg";

                var targetFileName = $"employee_{employee.Id}{extension}";
                var targetPath = Path.Combine(profileImagesFolder, targetFileName);

                File.Copy(sourcePath, targetPath, overwrite: true);
                employee.PhotoUrl = targetFileName;
                employee.UpdatedAt = DateTime.UtcNow;
                _repo.UpdateEmployee(employee);
            }
        }

        private static bool HasValidProfilePicture(Employee employee, string profileImagesFolder)
        {
            if (string.IsNullOrWhiteSpace(employee.PhotoUrl) ||
                !employee.PhotoUrl.StartsWith("employee_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = Path.GetFileName(employee.PhotoUrl);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return File.Exists(Path.Combine(profileImagesFolder, fileName));
        }
    }
}
