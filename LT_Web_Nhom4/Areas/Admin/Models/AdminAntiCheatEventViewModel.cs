using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminAntiCheatEventViewModel
    {
        public int Id { get; set; }

        public int ExamAttemptId { get; set; }

        public string ExamTitle { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public AntiCheatEventType EventType { get; set; }

        public AntiCheatSeverity Severity { get; set; }

        public string? Description { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
