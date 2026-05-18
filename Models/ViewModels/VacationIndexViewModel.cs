using System.Collections.Generic;

namespace PunchServerMVC.Models.ViewModels
{
    public class VacationIndexViewModel
    {
        public List<VacationType> VacationTypes { get; set; } = new();
        public List<VacationListItemViewModel> Vacations { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public int? SelectedEmployeeId { get; set; }
        public int? SelectedDepartmentId { get; set; }
        public System.DateTime FilterFrom { get; set; }
        public System.DateTime FilterTo { get; set; }
        public string ActiveTab { get; set; } = "vacations";
    }

    public class VacationListItemViewModel
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int VacationTypeId { get; set; }
        public string VacationTypeName { get; set; } = string.Empty;
        public string VacationTypeAbbreviation { get; set; } = string.Empty;
        public System.DateTime StartDate { get; set; }
        public System.DateTime EndDate { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }
}
