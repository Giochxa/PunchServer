using System;
using System.Collections.Generic;

namespace PunchServerMVC.Models
{
    public class ScheduleTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "Template";
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public string Type { get; set; } = "Regular";
    public int BreakMinutes { get; set; } = 0;

    // ✅ ADD THIS:
    public List<DayOfWeek> Days { get; set; } = new();
}

}
