<<<<<<< HEAD
using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Data
{
    public class PunchRepository : IRepository
    {
        private const string DatabasePath = "Punchdata.db";
        private readonly LiteDatabase _db;

        public PunchRepository()
        {
            _db = new LiteDatabase(DatabasePath);

            // ✅ Ensure unique indexes for Employee identifiers
            var employees = _db.GetCollection<Employee>();
            employees.EnsureIndex(e => e.PersonalId, unique: true);
            employees.EnsureIndex(e => e.UniqueId, unique: true);
        }

        public IEnumerable<Employee> GetEmployees()
        {
            return _db.GetCollection<Employee>().FindAll();
        }

        public Employee? GetEmployeeByCode(string code)
        {
            return _db.GetCollection<Employee>().FindOne(e => e.UniqueId == code);
        }

        public void AddPunch(Punch punch)
        {
            var punches = _db.GetCollection<Punch>();
            punches.Insert(punch);
        }

        public List<Punch> GetAllPunches()
        {
            var punches = _db.GetCollection<Punch>().FindAll().ToList();
            var employees = _db.GetCollection<Employee>().FindAll().ToDictionary(e => e.Id);

            foreach (var punch in punches)
            {
                if (employees.TryGetValue(punch.EmployeeId, out var employee))
                {
                    punch.Employee = employee;
                }
            }

            return punches;
        }

        // ✅ Add employee with duplicate protection
        public void AddEmployee(Employee emp)
{
    var employees = _db.GetCollection<Employee>();

    // Pre-check to show friendly errors before insert
    if (employees.Exists(e => e.PersonalId == emp.PersonalId))
        throw new InvalidOperationException("Duplicate PersonalId detected.");

    if (employees.Exists(e => e.UniqueId == emp.UniqueId))
        throw new InvalidOperationException("Duplicate UniqueId detected.");

    try
    {
        employees.Insert(emp);
    }
    catch (LiteException ex) when (ex.Message.Contains("duplicate"))
    {
        throw new InvalidOperationException("A record with the same PersonalId or UniqueId already exists.", ex);
    }
}

        
public void UpdateEmployee(Employee employee)
{
    var col = _db.GetCollection<Employee>();

    // ✅ Check for duplicates when updating
    if (col.Exists(e => e.Id != employee.Id && e.PersonalId == employee.PersonalId))
        throw new InvalidOperationException("Another employee already has this PersonalId.");

    if (col.Exists(e => e.Id != employee.Id && e.UniqueId == employee.UniqueId))
        throw new InvalidOperationException("Another employee already has this UniqueId.");

    col.Update(employee);
}

        public void DeleteEmployee(int id)
        {
            var col = _db.GetCollection<Employee>();
            col.Delete(id);
        }

        public IEnumerable<Schedule> GetSchedules()
        {
            return _db.GetCollection<Schedule>().FindAll();
        }

        public void AddSchedule(Schedule schedule)
        {
            _db.GetCollection<Schedule>().Insert(schedule);
        }

        public void UpdateSchedule(Schedule schedule)
        {
            _db.GetCollection<Schedule>().Update(schedule);
        }

        public void DeleteSchedule(int id)
        {
            _db.GetCollection<Schedule>().Delete(id);
        }

        public IEnumerable<ScheduleTemplate> GetTemplates() => _db.GetCollection<ScheduleTemplate>().FindAll();

        public void AddTemplate(ScheduleTemplate template) => _db.GetCollection<ScheduleTemplate>().Insert(template);

        public void DeleteTemplate(int id) => _db.GetCollection<ScheduleTemplate>().Delete(id);

        public void UpdateTemplate(ScheduleTemplate template)
        {
            _db.GetCollection<ScheduleTemplate>().Update(template);
        }

        public IEnumerable<Punch> GetPunches()
        {
            var punches = _db.GetCollection<Punch>().FindAll().ToList();
            var employees = _db.GetCollection<Employee>().FindAll().ToDictionary(e => e.Id);

            foreach (var punch in punches)
            {
                if (employees.TryGetValue(punch.EmployeeId, out var employee))
                {
                    punch.Employee = employee;
                }
            }

            return punches;
        }

        public List<Department> GetDepartments()
        {
            return _db.GetCollection<Department>("departments").FindAll().ToList();
        }

        public Department? GetDepartmentById(int id)
        {
            return _db.GetCollection<Department>("departments").FindById(id);
        }

        public void AddDepartment(Department department)
        {
            _db.GetCollection<Department>("departments").Insert(department);
        }

        public void UpdateDepartment(Department department)
        {
            _db.GetCollection<Department>("departments").Update(department);
        }

        public void DeleteDepartment(int id)
        {
            _db.GetCollection<Department>("departments").Delete(id);
        }

        public List<Organisation> GetOrganisations()
        {
            return _db.GetCollection<Organisation>("organisations").FindAll().ToList();
        }

        public Organisation? GetOrganisationById(int id)
        {
            return _db.GetCollection<Organisation>("organisations").FindById(id);
        }

        public void AddOrganisation(Organisation organisation)
        {
            _db.GetCollection<Organisation>("organisations").Insert(organisation);
        }

        public void UpdateOrganisation(Organisation organisation)
        {
            _db.GetCollection<Organisation>("organisations").Update(organisation);
        }

        public void DeleteOrganisation(int id)
        {
            _db.GetCollection<Organisation>("organisations").Delete(id);
        }

        public Dictionary<DateTime, List<CalendarEntry>> GetCalendarEntriesForMonth(
            int year,
            int month,
            int? employeeId,
            int? organisationId,
            int? departmentId)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var allEmployees = GetEmployees().ToDictionary(e => e.Id);

            var punches = GetPunches()
                .Where(p => p.Timestamp.Date >= monthStart && p.Timestamp.Date <= monthEnd);

            var schedules = GetSchedules()
                .Where(s => s.StartDate <= monthEnd && s.EndDate >= monthStart)
                .ToList();

            foreach (var schedule in schedules)
            {
                if (allEmployees.TryGetValue(schedule.EmployeeId, out var emp))
                {
                    schedule.Employee = emp;
                }
            }

            if (employeeId.HasValue)
            {
                punches = punches.Where(p => p.EmployeeId == employeeId.Value);
                schedules = schedules.Where(s => s.EmployeeId == employeeId.Value).ToList();
            }

            if (organisationId.HasValue)
            {
                punches = punches.Where(p => p.Employee?.OrganisationId == organisationId.Value);
                schedules = schedules.Where(s => s.Employee?.OrganisationId == organisationId.Value).ToList();
            }

            if (departmentId.HasValue)
            {
                punches = punches.Where(p => p.Employee?.DepartmentId == departmentId.Value);
                schedules = schedules.Where(s => s.Employee?.DepartmentId == departmentId.Value).ToList();
            }

            var entries = new Dictionary<DateTime, List<CalendarEntry>>();
            var scheduleMap = schedules.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var grace = TimeSpan.FromMinutes(5);

            foreach (var punchGroup in punches.GroupBy(p => new { p.EmployeeId, p.Timestamp.Date }))
            {
                var date = punchGroup.Key.Date;
                var empId = punchGroup.Key.EmployeeId;
                var empName = punchGroup.First().Employee?.FullName ?? "Unknown";

                if (!entries.ContainsKey(date))
                    entries[date] = new List<CalendarEntry>();

                Schedule? schedule = null;
                if (scheduleMap.TryGetValue(empId, out var empSchedules))
                {
                    schedule = empSchedules.FirstOrDefault(s =>
                        s.Days.Contains(date.DayOfWeek) &&
                        s.StartDate <= date &&
                        s.EndDate >= date);
                }

                var inPunch = punchGroup.MinBy(p => p.Timestamp);
                var outPunch = punchGroup.MaxBy(p => p.Timestamp);

                bool lateIn = schedule != null && inPunch.Timestamp.TimeOfDay > schedule.ShiftStart + grace;
                bool earlyOut = schedule != null && outPunch.Timestamp.TimeOfDay < schedule.ShiftEnd - grace;

                entries[date].Add(new CalendarEntry
                {
                    Date = date,
                    IsPunch = true,
                    EmployeeId = empId,
                    EmployeeName = empName,
                    PunchTime = inPunch.Timestamp,
                    IsLate = lateIn
                });

                if (inPunch != outPunch)
                {
                    entries[date].Add(new CalendarEntry
                    {
                        Date = date,
                        IsPunch = true,
                        EmployeeId = empId,
                        EmployeeName = empName,
                        PunchTime = outPunch.Timestamp,
                        IsLate = earlyOut
                    });
                }
            }

            foreach (var empSchedule in scheduleMap)
            {
                var empId = empSchedule.Key;
                if (!allEmployees.TryGetValue(empId, out var employee)) continue;

                foreach (var schedule in empSchedule.Value)
                {
                    var currentDate = schedule.StartDate.Date;
                    while (currentDate <= schedule.EndDate.Date)
                    {
                        if (schedule.Days.Contains(currentDate.DayOfWeek) &&
                            currentDate >= monthStart && currentDate <= monthEnd)
                        {
                            if (!entries.ContainsKey(currentDate))
                                entries[currentDate] = new List<CalendarEntry>();

                            entries[currentDate].Add(new CalendarEntry
                            {
                                Date = currentDate,
                                IsPunch = false,
                                EmployeeId = empId,
                                EmployeeName = employee.FullName,
                                ScheduleStart = schedule.ShiftStart,
                                ScheduleEnd = schedule.ShiftEnd
                            });
                        }

                        currentDate = currentDate.AddDays(1);
                    }
                }
            }

            return entries;
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Data
{
    public class PunchRepository : IRepository
    {
        private const string DatabaseFileName = "Punchdata.db";
        private static readonly string DatabaseFolderPath = Path.Combine(AppContext.BaseDirectory, "Punchdata");
        private static readonly string DatabasePath = ResolveDatabasePath();
        private static readonly string DatabaseLogPath = Path.Combine(DatabaseFolderPath, "Punchdata-log.db");
        private static readonly string BackupFolderPath = Path.Combine(DatabaseFolderPath, "Backup");
        private readonly LiteDatabase _db;

        public PunchRepository()
        {
            _db = new LiteDatabase(DatabasePath);

            // âœ… Ensure unique indexes for Employee identifiers
            var employees = _db.GetCollection<Employee>();
            employees.EnsureIndex(e => e.PersonalId, unique: true);
            employees.EnsureIndex(e => e.UniqueId, unique: true);

            var holidays = _db.GetCollection<Holiday>("holidays");
            holidays.EnsureIndex(h => h.Date, unique: true);

            var vacationTypes = _db.GetCollection<VacationType>("vacationTypes");
            vacationTypes.EnsureIndex(v => v.Name, unique: true);
            vacationTypes.EnsureIndex(v => v.Abbreviation, unique: true);

            var vacations = _db.GetCollection<Vacation>("vacations");
            vacations.EnsureIndex(v => v.EmployeeId);
            vacations.EnsureIndex(v => v.StartDate);
            vacations.EnsureIndex(v => v.EndDate);

            var administrationSettings = _db.GetCollection<AdministrationSettings>("administrationSettings");
            if (administrationSettings.FindById(1) == null)
            {
                administrationSettings.Insert(new AdministrationSettings());
            }

            var autoPunchOutRunLogs = _db.GetCollection<AutoPunchOutRunLog>("autoPunchOutRunLogs");
            autoPunchOutRunLogs.EnsureIndex(r => r.StartedAt);
        }

        private static string ResolveDatabasePath()
        {
            Directory.CreateDirectory(DatabaseFolderPath);

            var databasePath = Path.Combine(DatabaseFolderPath, DatabaseFileName);
            var oldSystem32FolderDatabasePath = Path.Combine(Environment.SystemDirectory, "Punchdata", DatabaseFileName);
            var oldSystem32DatabasePath = Path.Combine(Environment.SystemDirectory, DatabaseFileName);
            var oldWorkingDirectoryDatabasePath = Path.GetFullPath(DatabaseFileName);

            CopyDatabaseIfMissing(databasePath, oldSystem32FolderDatabasePath);
            CopyDatabaseIfMissing(databasePath, oldSystem32DatabasePath);
            CopyDatabaseIfMissing(databasePath, oldWorkingDirectoryDatabasePath);

            return databasePath;
        }

        private static void CopyDatabaseIfMissing(string targetPath, string sourcePath)
        {
            if (File.Exists(targetPath) || !File.Exists(sourcePath))
                return;

            File.Copy(sourcePath, targetPath);
        }

        public string CreateDatabaseBackupZip(DateTime backupAt)
        {
            Directory.CreateDirectory(BackupFolderPath);

            _db.Checkpoint();

            var backupPath = Path.Combine(
                BackupFolderPath,
                $"PunchdataBackup_{backupAt:yyyyMMdd_HHmmss}.zip");

            using var zipStream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            AddFileToArchive(archive, DatabasePath, DatabaseFileName);
            AddFileToArchive(archive, DatabaseLogPath, Path.GetFileName(DatabaseLogPath));

            return backupPath;
        }

        private static void AddFileToArchive(ZipArchive archive, string sourcePath, string entryName)
        {
            if (!File.Exists(sourcePath))
                return;

            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var entryStream = entry.Open();
            sourceStream.CopyTo(entryStream);
        }

        #region Employee Methods

        public IEnumerable<Employee> GetEmployees()
        {
            return _db.GetCollection<Employee>().FindAll();
        }

        public Employee? GetEmployeeByCode(string code)
        {
            return _db.GetCollection<Employee>().FindOne(e => e.UniqueId == code);
        }

        public void AddEmployee(Employee emp)
        {
            var employees = _db.GetCollection<Employee>();

            // Pre-check to show friendly errors before insert
            if (employees.Exists(e => e.PersonalId == emp.PersonalId))
                throw new InvalidOperationException("Duplicate PersonalId detected.");

            if (employees.Exists(e => e.UniqueId == emp.UniqueId))
                throw new InvalidOperationException("Duplicate UniqueId detected.");

            try
            {
                employees.Insert(emp);
            }
            catch (LiteException ex) when (ex.Message.Contains("duplicate"))
            {
                throw new InvalidOperationException("A record with the same PersonalId or UniqueId already exists.", ex);
            }
        }

        public void UpdateEmployee(Employee employee)
        {
            var col = _db.GetCollection<Employee>();

            // âœ… Check for duplicates when updating
            if (col.Exists(e => e.Id != employee.Id && e.PersonalId == employee.PersonalId))
                throw new InvalidOperationException("Another employee already has this PersonalId.");

            if (col.Exists(e => e.Id != employee.Id && e.UniqueId == employee.UniqueId))
                throw new InvalidOperationException("Another employee already has this UniqueId.");

            col.Update(employee);
        }

        public void DeleteEmployee(int id)
        {
            var col = _db.GetCollection<Employee>();
            col.Delete(id);
        }

        #endregion

        #region Punch Methods

        public void AddPunch(Punch punch)
        {
            var punches = _db.GetCollection<Punch>();
            punches.Insert(punch);
        }

        public List<Punch> GetAllPunches()
        {
            var punches = _db.GetCollection<Punch>().FindAll().ToList();
            var employees = _db.GetCollection<Employee>().FindAll().ToDictionary(e => e.Id);

            foreach (var punch in punches)
            {
                if (employees.TryGetValue(punch.EmployeeId, out var employee))
                {
                    punch.Employee = employee;
                }
            }

            return punches;
        }

        public IEnumerable<Punch> GetPunches()
        {
            var punches = _db.GetCollection<Punch>().FindAll().ToList();
            var employees = _db.GetCollection<Employee>().FindAll().ToDictionary(e => e.Id);

            foreach (var punch in punches)
            {
                if (employees.TryGetValue(punch.EmployeeId, out var employee))
                {
                    punch.Employee = employee;
                }
            }

            return punches;
        }

        public Punch? GetPunchById(int id)
        {
            return _db.GetCollection<Punch>().FindById(id);
        }

        public void UpdatePunch(Punch punch)
        {
            var col = _db.GetCollection<Punch>();
            col.Update(punch);
        }

        public void DeletePunch(int id)
        {
            var col = _db.GetCollection<Punch>();
            col.Delete(id);
        }

        public Dictionary<int, string> GetFirstPunchImagesForEmployees(IEnumerable<int> employeeIds)
        {
            var employeeIdSet = employeeIds?
                .Distinct()
                .ToHashSet() ?? new HashSet<int>();

            if (!employeeIdSet.Any())
                return new Dictionary<int, string>();

            return _db.GetCollection<Punch>()
                .Find(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
                .Where(p => employeeIdSet.Contains(p.EmployeeId))
                .OrderBy(p => p.Timestamp)
                .GroupBy(p => p.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First().ImageUrl);
        }

        #endregion

        #region Schedule Methods

        public IEnumerable<Schedule> GetSchedules()
        {
            return _db.GetCollection<Schedule>().FindAll();
        }

        public void AddSchedule(Schedule schedule)
        {
            _db.GetCollection<Schedule>().Insert(schedule);
        }

        public void UpdateSchedule(Schedule schedule)
        {
            _db.GetCollection<Schedule>().Update(schedule);
        }

        public void DeleteSchedule(int id)
        {
            _db.GetCollection<Schedule>().Delete(id);
        }

        #endregion

        #region Schedule Template Methods

        public IEnumerable<ScheduleTemplate> GetTemplates() => _db.GetCollection<ScheduleTemplate>().FindAll();

        public void AddTemplate(ScheduleTemplate template) => _db.GetCollection<ScheduleTemplate>().Insert(template);

        public void DeleteTemplate(int id) => _db.GetCollection<ScheduleTemplate>().Delete(id);

        public void UpdateTemplate(ScheduleTemplate template)
        {
            _db.GetCollection<ScheduleTemplate>().Update(template);
        }

        #endregion

        #region Department Methods

        public List<Department> GetDepartments()
        {
            return _db.GetCollection<Department>("departments").FindAll().ToList();
        }

        public Department? GetDepartmentById(int id)
        {
            return _db.GetCollection<Department>("departments").FindById(id);
        }

        public void AddDepartment(Department department)
        {
            _db.GetCollection<Department>("departments").Insert(department);
        }

        public void UpdateDepartment(Department department)
        {
            _db.GetCollection<Department>("departments").Update(department);
        }

        public void DeleteDepartment(int id)
        {
            _db.GetCollection<Department>("departments").Delete(id);
        }

        #endregion

        #region Organisation Methods

        public List<Organisation> GetOrganisations()
        {
            return _db.GetCollection<Organisation>("organisations").FindAll().ToList();
        }

        public Organisation? GetOrganisationById(int id)
        {
            return _db.GetCollection<Organisation>("organisations").FindById(id);
        }

        public void AddOrganisation(Organisation organisation)
        {
            _db.GetCollection<Organisation>("organisations").Insert(organisation);
        }

        public void UpdateOrganisation(Organisation organisation)
        {
            _db.GetCollection<Organisation>("organisations").Update(organisation);
        }

        public void DeleteOrganisation(int id)
        {
            _db.GetCollection<Organisation>("organisations").Delete(id);
        }

        #endregion

        #region Holiday Methods

        public IEnumerable<Holiday> GetHolidays()
        {
            return _db.GetCollection<Holiday>("holidays").FindAll();
        }

        public IEnumerable<Holiday> GetHolidaysInRange(DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;

            return _db.GetCollection<Holiday>("holidays")
                .Find(h => h.IsActive && h.Date >= fromDate && h.Date <= toDate);
        }

        public Holiday? GetHolidayById(int id)
        {
            return _db.GetCollection<Holiday>("holidays").FindById(id);
        }

        public void AddHoliday(Holiday holiday)
        {
            holiday.Date = holiday.Date.Date;
            _db.GetCollection<Holiday>("holidays").Insert(holiday);
        }

        public void UpdateHoliday(Holiday holiday)
        {
            holiday.Date = holiday.Date.Date;
            _db.GetCollection<Holiday>("holidays").Update(holiday);
        }

        public void DeleteHoliday(int id)
        {
            _db.GetCollection<Holiday>("holidays").Delete(id);
        }

        #endregion

        #region Vacation Type Methods

        public IEnumerable<VacationType> GetVacationTypes()
        {
            return _db.GetCollection<VacationType>("vacationTypes").FindAll();
        }

        public VacationType? GetVacationTypeById(int id)
        {
            return _db.GetCollection<VacationType>("vacationTypes").FindById(id);
        }

        public void AddVacationType(VacationType vacationType)
        {
            _db.GetCollection<VacationType>("vacationTypes").Insert(vacationType);
        }

        public void UpdateVacationType(VacationType vacationType)
        {
            _db.GetCollection<VacationType>("vacationTypes").Update(vacationType);
        }

        public void DeleteVacationType(int id)
        {
            _db.GetCollection<VacationType>("vacationTypes").Delete(id);
        }

        #endregion

        #region Vacation Methods

        public IEnumerable<Vacation> GetVacations()
        {
            return _db.GetCollection<Vacation>("vacations").FindAll();
        }

        public IEnumerable<Vacation> GetVacationsInRange(DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;

            return _db.GetCollection<Vacation>("vacations")
                .Find(v => v.IsActive && v.StartDate <= toDate && v.EndDate >= fromDate);
        }

        public Vacation? GetVacationById(int id)
        {
            return _db.GetCollection<Vacation>("vacations").FindById(id);
        }

        public void AddVacation(Vacation vacation)
        {
            vacation.StartDate = vacation.StartDate.Date;
            vacation.EndDate = vacation.EndDate.Date;
            _db.GetCollection<Vacation>("vacations").Insert(vacation);
        }

        public void UpdateVacation(Vacation vacation)
        {
            vacation.StartDate = vacation.StartDate.Date;
            vacation.EndDate = vacation.EndDate.Date;
            _db.GetCollection<Vacation>("vacations").Update(vacation);
        }

        public void DeleteVacation(int id)
        {
            _db.GetCollection<Vacation>("vacations").Delete(id);
        }

        #endregion

        #region Administration Methods

        public AdministrationSettings GetAdministrationSettings()
        {
            var collection = _db.GetCollection<AdministrationSettings>("administrationSettings");
            var settings = collection.FindById(1);
            if (settings != null)
            {
                if (settings.CalendarPunchGraceMinutes <= 0)
                    settings.CalendarPunchGraceMinutes = 15;

                if (settings.MissingInLookbackMinutes <= 0)
                    settings.MissingInLookbackMinutes = 120;

                return settings;
            }

            settings = new AdministrationSettings();
            collection.Upsert(settings);
            return settings;
        }

        public void SaveAdministrationSettings(AdministrationSettings settings)
        {
            settings.Id = 1;
            _db.GetCollection<AdministrationSettings>("administrationSettings").Upsert(settings);
        }

        public IEnumerable<AutoPunchOutRunLog> GetAutoPunchOutRunLogs(int take = 50)
        {
            return _db.GetCollection<AutoPunchOutRunLog>("autoPunchOutRunLogs")
                .FindAll()
                .OrderByDescending(r => r.StartedAt)
                .Take(take);
        }

        public AutoPunchOutRunLog? GetAutoPunchOutRunLogById(int id)
        {
            return _db.GetCollection<AutoPunchOutRunLog>("autoPunchOutRunLogs").FindById(id);
        }

        public void AddAutoPunchOutRunLog(AutoPunchOutRunLog runLog)
        {
            _db.GetCollection<AutoPunchOutRunLog>("autoPunchOutRunLogs").Insert(runLog);
        }

        #endregion

        #region Calendar Methods

        public Dictionary<DateTime, List<CalendarEntry>> GetCalendarEntriesForMonth(
            int year, int month, int? employeeId, int? organisationId, int? departmentId)
        {
            // âœ… Prevent ArgumentOutOfRangeException (invalid year/month coming from controller/querystring)
            var now = DateTime.Now;

            int safeYear = year;
            if (safeYear < 1 || safeYear > 9999)
                safeYear = now.Year;

            int safeMonth = month;
            if (safeMonth < 1 || safeMonth > 12)
                safeMonth = now.Month;

            var firstDay = new DateTime(safeYear, safeMonth, 1);
            var nextMonthFirstDay = firstDay.AddMonths(1);

            var filteredEmployees = _db.GetCollection<Employee>()
                .Find(e =>
                    (!organisationId.HasValue || e.OrganisationId == organisationId.Value) &&
                    (!departmentId.HasValue || e.DepartmentId == departmentId.Value) &&
                    (!employeeId.HasValue || e.Id == employeeId.Value))
                .ToList();
            var employeeIds = filteredEmployees.Select(e => e.Id).ToHashSet();
            var employeeLookup = filteredEmployees.ToDictionary(e => e.Id);

            // Build calendar entries
            var result = new Dictionary<DateTime, List<CalendarEntry>>();

            // If no employees match filters, return empty calendar data
            if (!employeeIds.Any())
                return result;

            // Get punches for the month
            var punches = _db.GetCollection<Punch>()
                .Find(p => p.Timestamp >= firstDay &&
                           p.Timestamp < nextMonthFirstDay)
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .ToList();

            // Get schedules for the month (overlap test with [firstDay, nextMonthFirstDay) window)
            var schedules = _db.GetCollection<Schedule>()
                .Find(s => s.StartDate < nextMonthFirstDay &&
                           s.EndDate >= firstDay)
                .Where(s => employeeIds.Contains(s.EmployeeId))
                .ToList();

            var vacationTypes = _db.GetCollection<VacationType>("vacationTypes")
                .FindAll()
                .ToDictionary(v => v.Id);

            var vacations = _db.GetCollection<Vacation>("vacations")
                .Find(v => v.IsActive &&
                           v.StartDate < nextMonthFirstDay &&
                           v.EndDate >= firstDay)
                .Where(v => employeeIds.Contains(v.EmployeeId))
                .ToList();

            // Add punch entries (UPDATED to include PunchId and PunchType)
            foreach (var punch in punches)
            {
                employeeLookup.TryGetValue(punch.EmployeeId, out var employee);
                var date = punch.Timestamp.Date;

                if (!result.ContainsKey(date))
                    result[date] = new List<CalendarEntry>();

                result[date].Add(new CalendarEntry
                {
                    Date = date,
                    IsPunch = true,
                    PunchId = punch.Id,
                    PunchTime = punch.Timestamp,
                    PunchType = punch.PunchType,
                    Note = punch.Note,
                    IsManualCreated = punch.IsManualCreated,
                    IsManualEdited = punch.IsManualEdited,
                    IsAutoPunchOut = punch.IsAutoPunchOut,
                    PunchImageUrl = string.IsNullOrWhiteSpace(punch.ImageUrl) ? null : punch.ImageUrl,
                    PunchCreatedAt = punch.CreatedAt,
                    EmployeeName = employee?.FullName ?? "Unknown",
                    EmployeeId = punch.EmployeeId
                });
            }

            // Add schedule entries
            foreach (var schedule in schedules)
            {
                employeeLookup.TryGetValue(schedule.EmployeeId, out var employee);
                var isNightShift = schedule.ShiftEnd < schedule.ShiftStart;

                // Include one day before the month so overnight shifts that start in the
                // previous month can still render their continuation on the first day here.
                for (var date = firstDay.AddDays(-1); date < nextMonthFirstDay; date = date.AddDays(1))
                {
                    var isScheduledDay =
                        (schedule.Days == null || schedule.Days.Count == 0 || schedule.Days.Contains(date.DayOfWeek)) &&
                        date.Date >= schedule.StartDate.Date &&
                        date.Date <= schedule.EndDate.Date;

                    if (isScheduledDay)
                    {
                        if (date >= firstDay)
                        {
                            if (!result.ContainsKey(date))
                                result[date] = new List<CalendarEntry>();

                            result[date].Add(new CalendarEntry
                            {
                                Date = date,
                                IsPunch = false,
                                ScheduleId = schedule.Id,
                                ScheduleStart = schedule.ShiftStart,
                                ScheduleEnd = isNightShift ? new TimeSpan(23, 59, 0) : schedule.ShiftEnd,
                                ScheduleType = schedule.ScheduleType,
                                ScheduleNote = schedule.Note,
                                IsNightShift = isNightShift,
                                IsScheduleContinuation = false,
                                EmployeeName = employee?.FullName ?? "Unknown",
                                EmployeeId = schedule.EmployeeId
                            });
                        }

                        if (isNightShift)
                        {
                            var nextDate = date.AddDays(1);
                            if (nextDate >= firstDay && nextDate < nextMonthFirstDay)
                            {
                                if (!result.ContainsKey(nextDate))
                                    result[nextDate] = new List<CalendarEntry>();

                                result[nextDate].Add(new CalendarEntry
                                {
                                    Date = nextDate,
                                    IsPunch = false,
                                    ScheduleId = schedule.Id,
                                    ScheduleStart = TimeSpan.Zero,
                                    ScheduleEnd = schedule.ShiftEnd,
                                    ScheduleType = schedule.ScheduleType,
                                    ScheduleNote = schedule.Note,
                                    IsNightShift = isNightShift,
                                    IsScheduleContinuation = true,
                                    EmployeeName = employee?.FullName ?? "Unknown",
                                    EmployeeId = schedule.EmployeeId
                                });
                            }
                        }
                    }
                }
            }

            foreach (var vacation in vacations)
            {
                employeeLookup.TryGetValue(vacation.EmployeeId, out var employee);
                vacationTypes.TryGetValue(vacation.VacationTypeId, out var vacationType);

                var from = vacation.StartDate.Date < firstDay ? firstDay : vacation.StartDate.Date;
                var to = vacation.EndDate.Date >= nextMonthFirstDay ? nextMonthFirstDay.AddDays(-1) : vacation.EndDate.Date;

                for (var date = from; date <= to; date = date.AddDays(1))
                {
                    if (!result.ContainsKey(date))
                        result[date] = new List<CalendarEntry>();

                    result[date].Add(new CalendarEntry
                    {
                        Date = date,
                        IsVacation = true,
                        VacationId = vacation.Id,
                        VacationTypeId = vacation.VacationTypeId,
                        VacationTypeName = vacationType?.Name ?? "Vacation",
                        VacationTypeAbbreviation = vacationType?.Abbreviation ?? "VAC",
                        VacationStartDate = vacation.StartDate.Date,
                        VacationEndDate = vacation.EndDate.Date,
                        VacationNotes = vacation.Notes,
                        EmployeeName = employee?.FullName ?? "Unknown",
                        EmployeeId = vacation.EmployeeId
                    });
                }
            }

            return result;
        }

        #endregion
    }
}


>>>>>>> master
