namespace LT_Web_Nhom4.Models
{
    public class Exam
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public int ClassId { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public decimal? PassingScore { get; set; }

        public decimal MaxScore { get; set; } = 10;

        public bool ShuffleQuestions { get; set; }

        public bool ShuffleOptions { get; set; }

        public bool RequireFullscreen { get; set; }

        public int? MaxTabSwitchCount { get; set; }

        public ExamStatus Status { get; set; } = ExamStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Subject Subject { get; set; } = null!;

        public Class Class { get; set; } = null!;

        public ApplicationUser CreatedBy { get; set; } = null!;

        public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

        public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
    }
}
