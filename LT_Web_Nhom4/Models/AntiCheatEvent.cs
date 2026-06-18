namespace LT_Web_Nhom4.Models
{
    public class AntiCheatEvent
    {
        public int Id { get; set; }

        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; }

        public AntiCheatSeverity Severity { get; set; } = AntiCheatSeverity.Low;

        public string? Description { get; set; }

        public string? MetadataJson { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public ExamAttempt ExamAttempt { get; set; } = null!;
    }
}
