using System;
using System.Collections.Generic;
using PunchServerMVC.Models;
using PunchServerMVC.Models.ViewModels;

namespace PunchServerMVC.Data
{
    public interface IRepository
    {
        // Punch methods
        void AddPunch(Punch punch);
        List<Punch> GetAllPunches();
        IEnumerable<Punch> GetPunches();
        Punch? GetPunchById(int id);
        void UpdatePunch(Punch punch);
        void DeletePunch(int id);
        Dictionary<int, string> GetFirstPunchImagesForEmployees(IEnumerable<int> employeeIds);

        // Employee methods
        IEnumerable<Employee> GetEmployees();
        Employee? GetEmployeeByCode(string code);
        void AddEmployee(Employee emp);
        void UpdateEmployee(Employee employee);
        void DeleteEmployee(int id);

        // Schedule methods
        IEnumerable<Schedule> GetSchedules();
        void AddSchedule(Schedule schedule);
        void UpdateSchedule(Schedule schedule);
        void DeleteSchedule(int id);

        // Schedule Template methods
        IEnumerable<ScheduleTemplate> GetTemplates();
        void AddTemplate(ScheduleTemplate template);
        void DeleteTemplate(int id);
        void UpdateTemplate(ScheduleTemplate template);

        // Department methods
        List<Department> GetDepartments();
        Department? GetDepartmentById(int id);
        void AddDepartment(Department department);
        void UpdateDepartment(Department department);
        void DeleteDepartment(int id);

        // Organisation methods
        List<Organisation> GetOrganisations();
        Organisation? GetOrganisationById(int id);
        void AddOrganisation(Organisation organisation);
        void UpdateOrganisation(Organisation organisation);
        void DeleteOrganisation(int id);

        // Holiday methods
        IEnumerable<Holiday> GetHolidays();
        IEnumerable<Holiday> GetHolidaysInRange(DateTime from, DateTime to);
        Holiday? GetHolidayById(int id);
        void AddHoliday(Holiday holiday);
        void UpdateHoliday(Holiday holiday);
        void DeleteHoliday(int id);

        // Vacation type methods
        IEnumerable<VacationType> GetVacationTypes();
        VacationType? GetVacationTypeById(int id);
        void AddVacationType(VacationType vacationType);
        void UpdateVacationType(VacationType vacationType);
        void DeleteVacationType(int id);

        // Vacation methods
        IEnumerable<Vacation> GetVacations();
        IEnumerable<Vacation> GetVacationsInRange(DateTime from, DateTime to);
        Vacation? GetVacationById(int id);
        void AddVacation(Vacation vacation);
        void UpdateVacation(Vacation vacation);
        void DeleteVacation(int id);

        // Administration settings / worker logs
        AdministrationSettings GetAdministrationSettings();
        void SaveAdministrationSettings(AdministrationSettings settings);
        string CreateDatabaseBackupZip(DateTime backupAt);
        IEnumerable<AutoPunchOutRunLog> GetAutoPunchOutRunLogs(int take = 50);
        AutoPunchOutRunLog? GetAutoPunchOutRunLogById(int id);
        void AddAutoPunchOutRunLog(AutoPunchOutRunLog runLog);

        // Calendar methods
        Dictionary<DateTime, List<CalendarEntry>> GetCalendarEntriesForMonth(
            int year, int month, int? employeeId, int? organisationId, int? departmentId);
    }
}
