using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ExamListViewModel
    {
        public IList<ExamCardViewModel> Exams { get; set; } = new List<ExamCardViewModel>();

        public IList<ExamCardViewModel> OwnedExams => Exams.Where(exam => exam.IsOwnedByCurrentUser).ToList();

        public IList<ExamCardViewModel> ParticipatingExams => Exams.Where(exam => !exam.IsOwnedByCurrentUser).ToList();
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

        public bool IsOwnedByCurrentUser { get; set; }

        public bool IsParticipant { get; set; }

        public string RoleLabel => IsOwnedByCurrentUser ? "Giao vien phong" : "Hoc sinh";
    }

    public class CreateExamInClassViewModel
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public string ClassCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề đề thi.")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; } = string.Empty;

        [Range(1, 600, ErrorMessage = "Thời lượng phải từ 1 đến 600 phút.")]
        [Display(Name = "Thời lượng")]
        public int DurationMinutes { get; set; } = 45;

        [Display(Name = "Bắt đầu")]
        public DateTime StartAt { get; set; } = DateTime.Now.AddHours(1);

        [Display(Name = "Kết thúc")]
        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(2);

        [Range(1, 1000, ErrorMessage = "Điểm tối đa phải lớn hơn 0.")]
        [Display(Name = "Điểm tối đa")]
        public decimal MaxScore { get; set; } = 10;

        [Display(Name = "Điểm đạt")]
        public decimal? PassingScore { get; set; }

        [Display(Name = "Trộn câu hỏi")]
        public bool ShuffleQuestions { get; set; } = true;

        [Display(Name = "Trộn đáp án")]
        public bool ShuffleOptions { get; set; } = true;

        [Display(Name = "Yêu cầu toàn màn hình")]
        public bool RequireFullscreen { get; set; }

        [Display(Name = "Số lần rời màn hình tối đa")]
        public int? MaxTabSwitchCount { get; set; } = 3;

        [Display(Name = "Trạng thái")]
        public ExamStatus Status { get; set; } = ExamStatus.Draft;

        public IList<ExamBuilderQuestionInputModel> Questions { get; set; } = new List<ExamBuilderQuestionInputModel>
        {
            ExamBuilderQuestionInputModel.CreateDefault()
        };
    }

    public class ExamBuilderQuestionInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
        [Display(Name = "Nội dung câu hỏi")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Loại câu hỏi")]
        public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;

        [Display(Name = "Điểm")]
        public decimal? Score { get; set; }

        [Display(Name = "Độ khó")]
        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        [Display(Name = "Đường dẫn ảnh")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Đường dẫn video")]
        public string? VideoUrl { get; set; }

        [Display(Name = "Giải thích")]
        public string? Explanation { get; set; }

        public IList<ExamBuilderOptionInputModel> Options { get; set; } = new List<ExamBuilderOptionInputModel>();

        public static ExamBuilderQuestionInputModel CreateDefault()
        {
            return new ExamBuilderQuestionInputModel
            {
                Options =
                {
                    new ExamBuilderOptionInputModel { Content = string.Empty, IsCorrect = true },
                    new ExamBuilderOptionInputModel { Content = string.Empty },
                    new ExamBuilderOptionInputModel { Content = string.Empty },
                    new ExamBuilderOptionInputModel { Content = string.Empty }
                }
            };
        }
    }

    public class ExamBuilderOptionInputModel
    {
        [Display(Name = "Đáp án")]
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
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

    public class ExamManageViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }

        public int ParticipantCount { get; set; }

        public int AttemptCount { get; set; }

        public int SubmittedCount { get; set; }

        public int WarningCount { get; set; }

        public decimal? AverageScore { get; set; }

        public IList<ExamParticipantViewModel> Participants { get; set; } = new List<ExamParticipantViewModel>();

        public IList<ExamAttemptSummaryViewModel> Attempts { get; set; } = new List<ExamAttemptSummaryViewModel>();

        public IList<ExamQuestionManageViewModel> Questions { get; set; } = new List<ExamQuestionManageViewModel>();
    }

    public class ExamParticipantViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    public class ExamAttemptSummaryViewModel
    {
        public int AttemptId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public string Status { get; set; } = string.Empty;
    }

    public class ExamQuestionManageViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public int OptionCount { get; set; }
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
