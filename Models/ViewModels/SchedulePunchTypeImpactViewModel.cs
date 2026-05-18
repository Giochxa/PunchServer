using System;
using System.Collections.Generic;

namespace PunchServerMVC.Models
{
    public sealed class PunchTypeImpactRow
    {
        public int PunchId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public string CurrentType { get; set; } = "";
        public string ProposedType { get; set; } = "";

        public bool IsAuto { get; set; }          // true => can be updated
        public string Reason { get; set; } = "";
    }

    public sealed class SchedulePunchTypeImpactViewModel
    {
        public int ScheduleId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan ShiftStart { get; set; }
        public TimeSpan ShiftEnd { get; set; }
        public string? ScheduleType { get; set; }

        public List<PunchTypeImpactRow> Rows { get; set; } = new();
    }
}
