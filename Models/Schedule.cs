using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PunchServerMVC.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }


        public int? ScheduleTemplateId { get; set; }

        [Required]
        [Display(Name = "Shift Start")]
        public TimeSpan ShiftStart { get; set; }

        [Required]
        [Display(Name = "Shift End")]
        public TimeSpan ShiftEnd { get; set; }

        [Range(0, 999)]
        [Display(Name = "Break Minutes")]
        public int BreakMinutes { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Schedule Type")]
        public string ScheduleType { get; set; } = "";

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public List<DayOfWeek> Days { get; set; } = new();
    }
}
