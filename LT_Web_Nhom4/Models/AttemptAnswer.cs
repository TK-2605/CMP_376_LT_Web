namespace LT_Web_Nhom4.Models
{
    public class AttemptAnswer
    {
        public int Id { get; set; }

        public int ExamAttemptId { get; set; }

        public int QuestionId { get; set; }

        public bool? IsCorrect { get; set; }

        public decimal? AwardedScore { get; set; }

        public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

        public ExamAttempt ExamAttempt { get; set; } = null!;

        public Question Question { get; set; } = null!;

        public ICollection<AttemptAnswerSelection> Selections { get; set; } = new List<AttemptAnswerSelection>();
    }
}
