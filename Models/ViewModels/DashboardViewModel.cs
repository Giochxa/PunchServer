using System;
using System.Collections.Generic;

namespace PunchServerMVC.Models.ViewModels
{
    public class DashboardViewModel
    {
        public DateTime Today { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string CurrentMonthLabel { get; set; } = string.Empty;
        public bool CanGoNextMonth { get; set; }
        public List<DashboardWidgetViewModel> Widgets { get; set; } = new();
    }

    public class DashboardWidgetViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsError { get; set; }
        public bool IsPositive { get; set; }
        public string ActionUrl { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? SecondaryInfo { get; set; }
    }

    public class DashboardDetailViewModel
    {
        public string WidgetKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public int Count { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime? FilterFrom { get; set; }
        public DateTime? FilterTo { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<DashboardDetailRowViewModel> Rows { get; set; } = new();
    }

    public class DashboardDetailRowViewModel
    {
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string TertiaryText { get; set; } = string.Empty;
        public string QuaternaryText { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string ActionText { get; set; } = "Open";
    }
}
