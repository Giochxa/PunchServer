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
