namespace LT_Web_Nhom4.Models
{
    public class AntiCheatEvent
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int ExamId { get; set; }

        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; }

        public AntiCheatSeverity Severity { get; set; } = AntiCheatSeverity.Low;

        public string? Description { get; set; }

        public string? MetadataJson { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public int ViolationCount { get; set; }

        public bool IsSuspicious { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? Note { get; set; }

        public ApplicationUser User { get; set; } = null!;

        public Exam Exam { get; set; } = null!;

        public ExamAttempt ExamAttempt { get; set; } = null!;
    }
}
