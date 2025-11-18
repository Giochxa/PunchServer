using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class MonthlyPunchReportViewModel
    {
        public int? OrganisationId { get; set; }
        public int? DepartmentId { get; set; }
        public int? EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }

        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();

        public List<MonthlyPunchRow> Rows { get; set; } = new();
    }

    public class MonthlyPunchRow
    {
        public string OrganisationName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string PersonalId { get; set; } = string.Empty;
        public List<DailyPunch> Days { get; set; } = new();
    }

    public class DailyPunch
    {
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public double? DifferenceHours { get; set; }
    }
}
