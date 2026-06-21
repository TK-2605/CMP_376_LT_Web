namespace LT_Web_Nhom4.Models
{
    public class Question
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public string? VideoUrl { get; set; }

        public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;

        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        public string? Explanation { get; set; }

        public QuestionStatus Status { get; set; } = QuestionStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public Subject Subject { get; set; } = null!;

        public ApplicationUser CreatedBy { get; set; } = null!;

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();

        public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

        public ICollection<AttemptAnswer> AttemptAnswers { get; set; } = new List<AttemptAnswer>();

        public ICollection<QuestionMedia> MediaAssets { get; set; } = new List<QuestionMedia>();
    }
}
