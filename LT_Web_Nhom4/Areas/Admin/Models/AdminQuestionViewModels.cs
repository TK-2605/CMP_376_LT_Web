using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminQuestionViewModel
    {
        public int Id { get; set; }

        public string SubjectName { get; set; } = string.Empty;

        public string CreatedByName { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public QuestionType QuestionType { get; set; }

        public QuestionDifficulty Difficulty { get; set; }

        public QuestionStatus Status { get; set; }

        public int OptionCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
