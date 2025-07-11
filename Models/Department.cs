//Department prameters
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;       
namespace PunchServerMVC.Models
{
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();

        public int? OrganisationId { get; set; }
    }
}