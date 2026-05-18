using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class MonthlyPunchReportViewModel
    {
        public string ActiveTab { get; set; } = "monthly";
        public int? OrganisationId { get; set; }
        public int? DepartmentId { get; set; }
        public int? EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int? Day { get; set; }
        public double? MinDifferenceHours { get; set; }
        public DateTime VacationFrom { get; set; }
        public DateTime VacationTo { get; set; }
        public DateTime LateFrom { get; set; }
        public DateTime LateTo { get; set; }
        public int? LateInMinutes { get; set; }
        public int? EarlyOutMinutes { get; set; }
        public int? OvertimeMinutes { get; set; }
        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();

        public List<MonthlyPunchRow> Rows { get; set; } = new();
        public List<VacationReportRow> VacationRows { get; set; } = new();
        public List<LateEarlyReportRow> LateRows { get; set; } = new();
        public List<SalaryExportRow> SalaryRows { get; set; } = new();
    }

    public class MonthlyPunchRow
    {
        public string OrganisationName { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string PersonalId { get; set; } = "";

        public List<DailyPunch> Days { get; set; } = new();
    }

    public class DailyPunch
    {
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public double? DifferenceHours { get; set; }
        public int DayNumber => PunchIn?.Day ?? PunchOut?.Day ?? 0;
    }

    public class VacationReportRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int UsedVacationDays { get; set; }
        public int AnnualVacationLimit { get; set; }
        public int RemainingVacationDays { get; set; }
    }

    public class LateEarlyReportRow
    {
        public DateTime WorkDate { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ScheduleText { get; set; } = string.Empty;
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public int LateInMinutes { get; set; }
        public int EarlyOutMinutes { get; set; }
        public int OvertimeMinutes { get; set; }
    }

    public class SalaryExportRow
    {
        public DateTime PeriodDate { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string ScheduleName { get; set; } = string.Empty;
        public string ChangeCommandType { get; set; } = string.Empty;
        public double? OvertimeHours { get; set; }
        public double? LateHours { get; set; }
        public string EmployeeWorkSchedule2 { get; set; } = string.Empty;
        public string VacationValue { get; set; } = string.Empty;
        public string AbsenceValue { get; set; } = string.Empty;
        public string WorkScheduleValue { get; set; } = string.Empty;
        public string SickLeaveValue { get; set; } = string.Empty;
        public double? ScheduledHours { get; set; }
        public double? WorkHours { get; set; }
        public double? BreakHours { get; set; }
        public DateTime? CalendarStart { get; set; }
        public double? CalendarHours { get; set; }
        public string AddedEmployeeWorkSchedule { get; set; } = string.Empty;
        public string ScheduleFromDocument { get; set; } = string.Empty;
        public string Part1 { get; set; } = string.Empty;
        public string Part2 { get; set; } = string.Empty;
        public string Part3 { get; set; } = string.Empty;
        public DateTime? CalendarEnd { get; set; }
        public int Active { get; set; } = 1;
        public string Release { get; set; } = string.Empty;
    }

}
