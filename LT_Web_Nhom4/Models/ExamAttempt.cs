namespace LT_Web_Nhom4.Models
{
    public class ExamAttempt
    {
        public int Id { get; set; }

        public int ExamId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;

        public bool IsAutoSubmitted { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? DeviceFingerprint { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Exam Exam { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        public ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();

        public ICollection<AntiCheatEvent> AntiCheatEvents { get; set; } = new List<AntiCheatEvent>();
    }
}
