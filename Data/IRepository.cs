using System;
using System.Collections.Generic;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Data
{
    public interface IRepository
    {
        void AddPunch(Punch punch);
        List<Punch> GetAllPunches();
        IEnumerable<Employee> GetEmployees();
        Employee? GetEmployeeByCode(string code);
        void AddEmployee(Employee emp);
        void UpdateEmployee(Employee employee);
        void DeleteEmployee(int id);
        IEnumerable<Schedule> GetSchedules();
        void AddSchedule(Schedule schedule);
        void UpdateSchedule(Schedule schedule);
        void DeleteSchedule(int id);
        IEnumerable<ScheduleTemplate> GetTemplates();
        void AddTemplate(ScheduleTemplate template);
        void DeleteTemplate(int id);
        void UpdateTemplate(ScheduleTemplate template);
        IEnumerable<Punch> GetPunches();

        // Department
        List<Department> GetDepartments();
        Department? GetDepartmentById(int id);
        void AddDepartment(Department department);
        void UpdateDepartment(Department department);
        void DeleteDepartment(int id);

        // Organisation
        List<Organisation> GetOrganisations();
        Organisation? GetOrganisationById(int id);
        void AddOrganisation(Organisation organisation);
        void UpdateOrganisation(Organisation organisation);
        void DeleteOrganisation(int id);
        Dictionary<DateTime, List<CalendarEntry>> GetCalendarEntriesForMonth(int year, int month, int? employeeId, int? organisationId, int? departmentId);



    }
}
