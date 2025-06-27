using System;
using System.Collections.Generic;

namespace PunchServerMVC.Models.ViewModels
{
    public class WorkSheetDayEntry
    {
        public int Day { get; set; }
        public string Status { get; set; } // e.g. ✔, x, V
    }

    public class EmployeeWorkSheet
    {
        public string FullName { get; set; }
        public string Position { get; set; }
        public string TabNumber { get; set; }

        public List<WorkSheetDayEntry> DailyEntries { get; set; }

        public double TotalWorkedHours { get; set; }
        public double WorkedHoursFirstHalf { get; set; }
        public double WorkedHoursSecondHalf { get; set; }

        public int AbsenceDays { get; set; }
        public double AbsenceHours { get; set; }
        public int WeekendDays { get; set; }

        public double BusinessTripHours { get; set; }
        public double PaidLeaveHours { get; set; }
        public double UnpaidLeaveHours { get; set; }
        public double OtherAbsenceHours { get; set; }

        public double OvertimeHours { get; set; }
        public double NightHours { get; set; }
        public double WeekendWorkedHours { get; set; }
        public double OtherHours { get; set; }
        public int WorkedDaysFirstHalf { get; set; }
        public int WorkedDaysSecondHalf { get; set; }
        public int TotalWorkedDays => WorkedDaysFirstHalf + WorkedDaysSecondHalf;


}


    public class WorkSheetViewModel
{
    public string OrganisationName { get; set; }
    public string Department { get; set; }
    public string IdentificationCode { get; set; }
    public DateTime FormDate { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime PeriodStart => new DateTime(Year, Month, 1);
    public DateTime PeriodEnd => new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month));

    public List<EmployeeWorkSheet> EmployeeSheets { get; set; }
}

}
