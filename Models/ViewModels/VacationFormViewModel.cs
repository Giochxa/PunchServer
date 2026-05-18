using System.Collections.Generic;

namespace PunchServerMVC.Models.ViewModels
{
    public class VacationFormViewModel
    {
        public Vacation Vacation { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<VacationType> VacationTypes { get; set; } = new();
    }
}
