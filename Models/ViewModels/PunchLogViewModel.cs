using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class PunchLogViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public string? PunchType { get; set; } // null/"" = all, "In", "Out"
        public string? PunchCondition { get; set; }
        public int? EmployeeId { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<Punch> Punches { get; set; } = new();
    }
}
