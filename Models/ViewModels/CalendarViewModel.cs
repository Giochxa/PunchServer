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
