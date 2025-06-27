using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class DepartmentFormViewModel
    {
        public Department Department { get; set; } = new();
        public List<Organisation> Organisations { get; set; } = new();
    }
}
