using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PunchServerMVC.Data;

namespace PunchServerMVC.Services
{
    public class DatabaseBackupWorker : BackgroundService
    {
        private readonly ILogger<DatabaseBackupWorker> _logger;
        private readonly IRepository _repo;

        public DatabaseBackupWorker(ILogger<DatabaseBackupWorker> logger, IRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RunIfDue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database backup worker failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private void RunIfDue()
        {
            var settings = _repo.GetAdministrationSettings();
            if (!settings.DailyBackupTimeCheck.HasValue)
                return;

            var now = DateTime.Now;
            if (now.TimeOfDay < settings.DailyBackupTimeCheck.Value)
                return;

            if (settings.LastDatabaseBackupRunAt.HasValue &&
                settings.LastDatabaseBackupRunAt.Value.Date == now.Date)
            {
                var lastRunCheckTime = settings.LastDatabaseBackupTimeCheck;
                if (lastRunCheckTime.HasValue &&
                    lastRunCheckTime.Value >= settings.DailyBackupTimeCheck.Value)
                {
                    return;
                }
            }

            var backupPath = _repo.CreateDatabaseBackupZip(now);
            settings.LastDatabaseBackupRunAt = now;
            settings.LastDatabaseBackupTimeCheck = settings.DailyBackupTimeCheck;
            _repo.SaveAdministrationSettings(settings);

            _logger.LogInformation("Database backup created at {BackupPath}.", backupPath);
        }
    }
}
