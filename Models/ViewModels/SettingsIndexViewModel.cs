using System;
using System.Collections.Generic;
using PunchServerMVC.Models;

namespace PunchServerMVC.Models.ViewModels
{
    public class SettingsIndexViewModel
    {
        public string ActiveTab { get; set; } = "holidays";
        public int SelectedYear { get; set; } = DateTime.Today.Year;
        public List<int> AvailableHolidayYears { get; set; } = new();
        public List<Holiday> Holidays { get; set; } = new();
        public List<VacationType> VacationTypes { get; set; } = new();
        public List<Organisation> Organisations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<ScheduleTemplate> Templates { get; set; } = new();
        public AdministrationSettings AdministrationSettings { get; set; } = new();
        public AutoPunchOutRunLog? LastAutoPunchOutRun { get; set; }
    }
}
