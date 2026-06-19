using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ExamListViewModel
    {
        public IList<ExamCardViewModel> Exams { get; set; } = new List<ExamCardViewModel>();
    }

    public class ExamCardViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }
    }

    public class ExamRoomViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }

        public DateTime Now { get; set; } = DateTime.Now;

        public bool IsOpen => Now >= StartAt && Now <= EndAt;

        public bool IsClosed => Now > EndAt;

        public TimeSpan TimeUntilStart => StartAt > Now ? StartAt - Now : TimeSpan.Zero;

        public IList<string> Rules { get; set; } = new List<string>
        {
            "Doc ky thong tin de thi truoc khi bat dau.",
            "Moi cau hoi chi chon mot dap an.",
            "Khong tat trinh duyet trong khi dang lam bai.",
            "Kiem tra tien do tra loi truoc khi nop bai."
        };
    }

    public class ExamStartViewModel
    {
        public int ExamId { get; set; }

        public int AttemptId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public DateTime StartedAt { get; set; }

        public IList<ExamQuestionViewModel> Questions { get; set; } = new List<ExamQuestionViewModel>();
    }

    public class ExamQuestionViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public IList<ExamOptionViewModel> Options { get; set; } = new List<ExamOptionViewModel>();
    }

    public class ExamOptionViewModel
    {
        public int OptionId { get; set; }

        public string Content { get; set; } = string.Empty;
    }

    public class ExamSubmitViewModel
    {
        [Required]
        public int ExamId { get; set; }

        [Required]
        public int AttemptId { get; set; }

        [MinLength(1)]
        public IList<AttemptAnswerInputModel> Answers { get; set; } = new List<AttemptAnswerInputModel>();
    }

    public class AttemptAnswerInputModel
    {
        [Required]
        public int QuestionId { get; set; }

        public int? SelectedOptionId { get; set; }
    }
}
