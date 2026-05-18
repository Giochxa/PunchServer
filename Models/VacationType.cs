using System;
using System.ComponentModel.DataAnnotations;

namespace PunchServerMVC.Models
{
    public class VacationType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Abbreviation { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? IntegrationValue { get; set; }

        public bool DoesNotCountAsUsedVacations { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
