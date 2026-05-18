using System;
using System.Collections.Generic;

namespace PunchServerMVC.Models
{
    public class AutoPunchOutRunLog
    {
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public List<AutoPunchOutRunLogItem> AddedPunches { get; set; } = new();
    }

    public class AutoPunchOutRunLogItem
    {
        public int PunchId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime PunchTimestamp { get; set; }
        public DateTime WorkDate { get; set; }
        public string ScheduleText { get; set; } = string.Empty;
    }
}
