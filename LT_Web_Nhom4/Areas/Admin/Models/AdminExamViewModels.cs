using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminExamSummaryViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public string CreatedByName { get; set; } = string.Empty;

        public ExamStatus Status { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int QuestionCount { get; set; }

        public int AttemptCount { get; set; }
    }

    public class AdminExamAttemptViewModel
    {
        public int Id { get; set; }

        public string ExamTitle { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public string StudentEmail { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public ExamAttemptStatus Status { get; set; }

        public int AntiCheatEventCount { get; set; }
    }
}
