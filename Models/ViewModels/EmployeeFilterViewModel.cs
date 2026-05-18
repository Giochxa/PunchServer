using System.Collections.Generic;

namespace PunchServerMVC.Models.ViewModels
{
    public class EmployeeFilterViewModel
    {
        public string? FullName { get; set; }
        public string? PersonalId { get; set; }
        public int? OrganisationId { get; set; }
        public int? DepartmentId { get; set; }
        public bool? IsActive { get; set; }

        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
    }
}
