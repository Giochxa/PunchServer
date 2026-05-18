using System;

namespace PunchServerMVC.Models
{
    public class AdministrationSettings
    {
        public int Id { get; set; } = 1;
        public bool AutoPunchOutEnabled { get; set; }
        public TimeSpan? AutoPunchOutTimeCheck { get; set; }
        public DateTime? LastAutoPunchOutRunAt { get; set; }
        public TimeSpan? LastAutoPunchOutRunTimeCheck { get; set; }
        public TimeSpan? DailyBackupTimeCheck { get; set; }
        public DateTime? LastDatabaseBackupRunAt { get; set; }
        public TimeSpan? LastDatabaseBackupTimeCheck { get; set; }
        public int CalendarPunchGraceMinutes { get; set; } = 15;
        public int MissingInLookbackMinutes { get; set; } = 120;
        public bool HidePunchImages { get; set; }
        public bool HideProfilePictures { get; set; }
    }
}
