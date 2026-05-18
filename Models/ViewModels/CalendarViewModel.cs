<<<<<<< HEAD
using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class CalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int CurrentYear => Year;
        public int CurrentMonth => Month;

        public int? SelectedEmployeeId { get; set; }
        public int? SelectedOrganisationId { get; set; }
        public int? SelectedDepartmentId { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();

        // Dictionary<DateOnly, List of entries (Punch or Schedule)>
        public Dictionary<DateTime, List<CalendarEntry>> CalendarEntries { get; set; } = new();
    }

    public class CalendarEntry
    {
        public DateTime Date { get; set; }
        public bool IsPunch { get; set; }

        // Common
        public string EmployeeName { get; set; }

        // For punches
        public DateTime PunchTime { get; set; }

        // For schedules
        public TimeSpan ScheduleStart { get; set; }
        public TimeSpan ScheduleEnd { get; set; }
        public int? EmployeeId { get; set; }
        public bool IsLate { get; set; } // NEW

    }

    public class CalendarDayInfo
    {
        public bool IsPunch { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public string ScheduleStart { get; set; } = string.Empty;
        public string ScheduleEnd { get; set; } = string.Empty;
    }
}
=======
using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class CalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int CurrentYear => Year;
        public int CurrentMonth => Month;

        public int? SelectedEmployeeId { get; set; }
        public int? SelectedOrganisationId { get; set; }
        public int? SelectedDepartmentId { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Holiday> Holidays { get; set; } = new();
        public List<VacationType> VacationTypes { get; set; } = new();

        // Dictionary<DateOnly, List of entries (Punch or Schedule)>
        public Dictionary<DateTime, List<CalendarEntry>> CalendarEntries { get; set; } = new();
        public Dictionary<DateTime, List<CalendarEmployeeGroup>> GroupedCalendarEntries { get; set; } = new();
    }

    public class CalendarEntry
    {
        public DateTime Date { get; set; }
        public bool IsPunch { get; set; }

        // Common
        public string EmployeeName { get; set; }

        // For punches
        public int? PunchId { get; set; } // ADDED for CRUD operations
        public DateTime PunchTime { get; set; }
        public string PunchType { get; set; } // ADDED for In/Out display
        public string? Note { get; set; }
        public bool IsManualCreated { get; set; }
        public bool IsManualEdited { get; set; }
        public bool IsAutoPunchOut { get; set; }
        public string? PunchImageUrl { get; set; }
        public DateTime? PunchCreatedAt { get; set; }

        // For schedules
        public int? ScheduleId { get; set; }
        public TimeSpan ScheduleStart { get; set; }
        public TimeSpan ScheduleEnd { get; set; }
        public string ScheduleType { get; set; } = string.Empty;
        public string? ScheduleNote { get; set; }
        public bool IsNightShift { get; set; }
        public bool IsScheduleContinuation { get; set; }
        public int? EmployeeId { get; set; }
        public bool IsLate { get; set; }

        // For vacations
        public bool IsVacation { get; set; }
        public int? VacationId { get; set; }
        public int? VacationTypeId { get; set; }
        public string VacationTypeName { get; set; } = string.Empty;
        public string VacationTypeAbbreviation { get; set; } = string.Empty;
        public DateTime? VacationStartDate { get; set; }
        public DateTime? VacationEndDate { get; set; }
        public string? VacationNotes { get; set; }
    }

    public class CalendarDayInfo
    {
        public bool IsPunch { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public string ScheduleStart { get; set; } = string.Empty;
        public string ScheduleEnd { get; set; } = string.Empty;
    }

    public class CalendarEmployeeGroup
    {
        public string EmployeeName { get; set; } = string.Empty;
        public int? EmployeeId { get; set; }
        public List<CalendarEntry> Punches { get; set; } = new();
        public List<CalendarEntry> Schedules { get; set; } = new();
        public List<CalendarEntry> Vacations { get; set; } = new();
    }
}
>>>>>>> master
