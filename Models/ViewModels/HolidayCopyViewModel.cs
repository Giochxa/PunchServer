using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PunchServerMVC.Models.ViewModels
{
    public class HolidayCopyViewModel
    {
        public int SourceYear { get; set; }
        public int TargetYear { get; set; }
        public List<HolidayCopyRowViewModel> Holidays { get; set; } = new();
    }

    public class HolidayCopyRowViewModel
    {
        public bool Include { get; set; } = true;
        public DateTime SourceDate { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public bool AlreadyExists { get; set; }
    }
}
