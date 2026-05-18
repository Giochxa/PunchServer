using System;
using System.Collections.Generic;
using PunchServerMVC.Utils;

namespace PunchServerMVC.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PersonalId { get; set; } = string.Empty;
        public string UniqueId { get; set; } = string.Empty;
        // Optional fields
        // Allowed values for Gender in UI: "მდედრობითი", "მამრობითი" (or null/empty)
        public string? Gender { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateTime? BirthDate { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public ICollection<Punch> Punches { get; set; } = new List<Punch>();
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public int? OrganisationId { get; set; }
        public int? DepartmentId { get; set; }
        public DateTime? EmploymentStartDate { get; set; }
        [DateGreaterThan(nameof(EmploymentStartDate), ErrorMessage = "End date must be after start date.")]
        public DateTime? EmploymentEndDate { get; set; }
        public string? Position { get; set; }
        public bool DoesNotNeedSchedule { get; set; }
        public int? VacationsPerYear { get; set; }
        public int? RemainingVacations { get; set; }
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateTime? RemainingVacationsFrom { get; set; }
    }
}
