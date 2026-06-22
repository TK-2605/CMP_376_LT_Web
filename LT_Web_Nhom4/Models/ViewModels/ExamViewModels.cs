using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ExamListViewModel
    {
        public DateTime Now { get; set; } = DateTime.Now;

        public IList<ExamCardViewModel> Exams { get; set; } = new List<ExamCardViewModel>();

        public IList<ExamCardViewModel> OwnedExams => Exams.Where(item => item.IsOwnedByCurrentUser).ToList();

        public IList<ExamCardViewModel> ParticipatingExams => Exams.Where(item => !item.IsOwnedByCurrentUser).ToList();
    }

    public class ExamCardViewModel
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }

        public ExamStatus Status { get; set; }

        public bool IsOwnedByCurrentUser { get; set; }

        public bool IsParticipant { get; set; }

        public int? CurrentUserAttemptId { get; set; }

        public DateTime? CurrentUserSubmittedAt { get; set; }

        public ExamAttemptStatus? CurrentUserAttemptStatus { get; set; }

        public bool HasCompletedAttempt => CurrentUserSubmittedAt.HasValue
            || CurrentUserAttemptStatus is ExamAttemptStatus.Submitted or ExamAttemptStatus.AutoSubmitted;
    }

    public class ExamBuilderViewModel
    {
        public int? ExamId { get; set; }

        public int ClassId { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public string ClassCode { get; set; } = string.Empty;

        public string ExamCode { get; set; } = string.Empty;

        public bool IsLocked { get; set; }

        public string ActivePanel { get; set; } = "questions";

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required(ErrorMessage = "Vui lòng nhập tên đề thi.")]
        [StringLength(200, ErrorMessage = "Tên đề thi không được vượt quá 200 ký tự.")]
        [Display(Name = "Tên đề thi")]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000, ErrorMessage = "Hướng dẫn không được vượt quá 4.000 ký tự.")]
        [Display(Name = "Hướng dẫn làm bài")]
        public string? Instructions { get; set; }

        [Range(1, 600, ErrorMessage = "Thời lượng phải từ 1 đến 600 phút.")]
        [Display(Name = "Thời lượng")]
        public int DurationMinutes { get; set; } = 45;

        [Display(Name = "Bắt đầu")]
        public DateTime StartAt { get; set; } = DateTime.Now.AddHours(1);

        [Display(Name = "Kết thúc")]
        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(2);

        [Range(0.01, 1000, ErrorMessage = "Điểm tối đa phải lớn hơn 0.")]
        [Display(Name = "Điểm tối đa")]
        public decimal MaxScore { get; set; } = 10;

        [Range(0, 1000, ErrorMessage = "Điểm đạt không hợp lệ.")]
        [Display(Name = "Điểm đạt")]
        public decimal? PassingScore { get; set; }

        [Display(Name = "Trộn câu hỏi")]
        public bool ShuffleQuestions { get; set; } = true;

        [Display(Name = "Trộn đáp án")]
        public bool ShuffleOptions { get; set; } = true;

        [Display(Name = "Yêu cầu toàn màn hình")]
        public bool RequireFullscreen { get; set; }

        [Range(1, 20, ErrorMessage = "Giới hạn cảnh báo phải từ 1 đến 20.")]
        [Display(Name = "Giới hạn cảnh báo")]
        public int? MaxWarningCount { get; set; } = 3;

        [Display(Name = "Công bố kết quả")]
        public ResultReleaseMode ResultReleaseMode { get; set; } = ResultReleaseMode.AfterExamClosed;

        public IList<ExamBuilderQuestionInputModel> Questions { get; set; } = new List<ExamBuilderQuestionInputModel>
        {
            ExamBuilderQuestionInputModel.CreateDefault()
        };
    }

    public class ExamBuilderQuestionInputModel
    {
        public int? QuestionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
        [StringLength(4000, ErrorMessage = "Câu hỏi không được vượt quá 4.000 ký tự.")]
        public string Content { get; set; } = string.Empty;

        public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;

        [Range(0.01, 1000, ErrorMessage = "Điểm câu hỏi phải lớn hơn 0.")]
        public decimal? Score { get; set; }

        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        public IFormFile? ImageFile { get; set; }

        public string? ExistingImageUrl { get; set; }

        public bool RemoveImage { get; set; }

        public IFormFileCollection? ImageFiles { get; set; }

        public IFormFileCollection? VideoFiles { get; set; }

        public IList<MediaAssetViewModel> ExistingMedia { get; set; } = new List<MediaAssetViewModel>();

        public IList<int> RemoveMediaIds { get; set; } = new List<int>();

        [Url(ErrorMessage = "Liên kết video không hợp lệ.")]
        public string? VideoUrl { get; set; }

        [StringLength(4000, ErrorMessage = "Giải thích không được vượt quá 4.000 ký tự.")]
        public string? Explanation { get; set; }

        public IList<ExamBuilderOptionInputModel> Options { get; set; } = new List<ExamBuilderOptionInputModel>();

        public static ExamBuilderQuestionInputModel CreateDefault()
        {
            return new ExamBuilderQuestionInputModel
            {
                Options =
                {
                    new ExamBuilderOptionInputModel { IsCorrect = true },
                    new ExamBuilderOptionInputModel(),
                    new ExamBuilderOptionInputModel(),
                    new ExamBuilderOptionInputModel()
                }
            };
        }
    }

    public class ExamBuilderOptionInputModel
    {
        public int? OptionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }

    public class ExamRoomViewModel
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public string? CoverImageUrl { get; set; }

        public string? Instructions { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }

        public decimal MaxScore { get; set; }

        public bool RequireFullscreen { get; set; }

        public int? MaxWarningCount { get; set; }

        public DateTime Now { get; set; } = DateTime.Now;

        public bool IsOpen => Now >= StartAt && Now <= EndAt;

        public bool IsClosed => Now > EndAt;

        public TimeSpan TimeUntilStart => StartAt > Now ? StartAt - Now : TimeSpan.Zero;
    }

    public class ExamManageViewModel
    {
        public int Id { get; set; }

        public int ClassId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public ExamStatus Status { get; set; }

        public ResultReleaseMode ResultReleaseMode { get; set; }

        public bool ResultsReleased { get; set; }

        public bool CanEditContent { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public int DurationMinutes { get; set; }

        public decimal MaxScore { get; set; }

        public int? MaxWarningCount { get; set; }

        public int QuestionCount { get; set; }

        public int ParticipantCount { get; set; }

        public int AttemptCount { get; set; }

        public int SubmittedCount { get; set; }

        public int WarningCount { get; set; }

        public decimal? AverageScore { get; set; }

        public IList<ExamParticipantViewModel> Participants { get; set; } = new List<ExamParticipantViewModel>();

        public IList<ExamAttemptSummaryViewModel> Attempts { get; set; } = new List<ExamAttemptSummaryViewModel>();

        public IList<ExamQuestionManageViewModel> Questions { get; set; } = new List<ExamQuestionManageViewModel>();

        public IList<AntiCheatEventItemViewModel> Warnings { get; set; } = new List<AntiCheatEventItemViewModel>();
    }

    public class ExamParticipantViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }

    public class ExamAttemptSummaryViewModel
    {
        public int AttemptId { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public ExamAttemptStatus Status { get; set; }

        public int WarningCount { get; set; }
    }

    public class ExamQuestionManageViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public int OptionCount { get; set; }
    }

    public class AntiCheatEventItemViewModel
    {
        public string StudentName { get; set; } = string.Empty;

        public AntiCheatEventType EventType { get; set; }

        public DateTime OccurredAt { get; set; }
    }

    public class ExamStartViewModel
    {
        public int ExamId { get; set; }

        public int AttemptId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool RequireFullscreen { get; set; }

        public int WarningCount { get; set; }

        public int? MaxWarningCount { get; set; }

        public IList<ExamQuestionViewModel> Questions { get; set; } = new List<ExamQuestionViewModel>();
    }

    public class ExamQuestionViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public string? VideoUrl { get; set; }

        public IList<MediaAssetViewModel> MediaAssets { get; set; } = new List<MediaAssetViewModel>();

        public QuestionType QuestionType { get; set; }

        public decimal Score { get; set; }

        public IList<int> SelectedOptionIds { get; set; } = new List<int>();

        public IList<ExamOptionViewModel> Options { get; set; } = new List<ExamOptionViewModel>();
    }

    public class ExamOptionViewModel
    {
        public int OptionId { get; set; }

        public string Content { get; set; } = string.Empty;
    }

    public class AttemptAnswerInputModel
    {
        [Required]
        public int AttemptId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public IList<int> SelectedOptionIds { get; set; } = new List<int>();
    }

    public class ExamSubmitViewModel
    {
        [Required]
        public int AttemptId { get; set; }
    }

    public class AntiCheatEventInputModel
    {
        [Required]
        public int AttemptId { get; set; }

        [Required]
        public AntiCheatEventType EventType { get; set; }
    }

    public class ExamResultViewModel
    {
        public int AttemptId { get; set; }

        public string ExamTitle { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public decimal? Score { get; set; }

        public decimal MaxScore { get; set; }

        public decimal? PassingScore { get; set; }

        public int CorrectCount { get; set; }

        public int QuestionCount { get; set; }

        public bool IsReleased { get; set; }

        public bool IsAutoSubmitted { get; set; }

        public DateTime? SubmittedAt { get; set; }
    }
}
