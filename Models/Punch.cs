using System;

namespace PunchServerMVC.Models
{
    public class Punch
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string PunchType { get; set; } = "Unknown"; // In, Out
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public string ImageUrl { get; set; } = string.Empty; // URL to the punch image
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ✅ NEW: manual tracking
        public bool IsManualCreated { get; set; } = false;
        public bool IsManualEdited { get; set; } = false;
        public bool IsAutoPunchOut { get; set; } = false;
        public DateTime? UpdatedAt { get; set; }
        public string? Note { get; set; }

    }
}
