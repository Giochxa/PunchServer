using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class EmployeeFormViewModel
    {
        public Employee Employee { get; set; } = new();
        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public PunchLogViewModel PunchLog { get; set; } = new();
        public string? EffectivePhotoUrl { get; set; }
    }
}
