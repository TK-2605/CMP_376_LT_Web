using System.ComponentModel.DataAnnotations;
using LT_Web_Nhom4.Models;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ApiTestPlanResponse
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string Mode { get; set; } = "Localhost Development";

        public IReadOnlyList<ApiTestGroup> Groups { get; set; } = Array.Empty<ApiTestGroup>();
    }

    public class ApiTestGroup
    {
        public string Name { get; set; } = string.Empty;

        public string Purpose { get; set; } = string.Empty;

        public IReadOnlyList<ApiTestStep> Steps { get; set; } = Array.Empty<ApiTestStep>();
    }

    public class ApiTestStep
    {
        public string Method { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string TestWhat { get; set; } = string.Empty;

        public bool NeedsBearerToken { get; set; }

        public string? Note { get; set; }
    }

    public class DatabaseSummaryResponse
    {
        public string Provider { get; set; } = string.Empty;

        public string Environment { get; set; } = string.Empty;

        public bool CanConnect { get; set; }

        public IReadOnlyList<DatabaseSummaryTable> Tables { get; set; } = Array.Empty<DatabaseSummaryTable>();
    }

    public class DatabaseSummaryTable
    {
        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class SubjectDto
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public class SubjectUpsertRequest
    {
        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public class ClassDto
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public string TeacherId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Semester { get; set; }

        public string? AcademicYear { get; set; }

        public string? IntroVideoUrl { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ClassUpsertRequest
    {
        [Required]
        public int SubjectId { get; set; }

        public string? TeacherId { get; set; }

        [StringLength(50)]
        public string? Code { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Semester { get; set; }

        [StringLength(50)]
        public string? AcademicYear { get; set; }

        [StringLength(1000)]
        public string? IntroVideoUrl { get; set; }
    }

    public class ClassMemberDto
    {
        public int ClassId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public DateTime JoinedAt { get; set; }

        public ClassMemberStatus Status { get; set; }
    }

    public class ClassMemberUpsertRequest
    {
        [Required]
        public int ClassId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ClassMemberStatus Status { get; set; } = ClassMemberStatus.Active;
    }

    public class QuestionDto
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? VideoUrl { get; set; }

        public QuestionType QuestionType { get; set; }

        public QuestionDifficulty Difficulty { get; set; }

        public string? Explanation { get; set; }

        public QuestionStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class QuestionUpsertRequest
    {
        [Required]
        public int SubjectId { get; set; }

        public string? CreatedById { get; set; }

        [Required, StringLength(4000)]
        public string Content { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? VideoUrl { get; set; }

        public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;

        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        [StringLength(4000)]
        public string? Explanation { get; set; }

        public QuestionStatus Status { get; set; } = QuestionStatus.Published;
    }

    public class QuestionOptionDto
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int DisplayOrder { get; set; }
    }

    public class QuestionOptionUpsertRequest
    {
        [Required]
        public int QuestionId { get; set; }

        [Required, StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int DisplayOrder { get; set; } = 1;
    }

    public class ExamDto
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public int ClassId { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public decimal? PassingScore { get; set; }

        public decimal MaxScore { get; set; }

        public ExamStatus Status { get; set; }
    }

    public class ExamUpsertRequest
    {
        [Required]
        public int SubjectId { get; set; }

        [Required]
        public int ClassId { get; set; }

        public string? CreatedById { get; set; }

        [StringLength(20)]
        public string? Code { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Instructions { get; set; }

        [Range(1, 10000)]
        public int DurationMinutes { get; set; } = 45;

        public DateTime StartAt { get; set; } = DateTime.Now.AddHours(1);

        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(2);

        public decimal? PassingScore { get; set; }

        public decimal MaxScore { get; set; } = 10;

        public bool ShuffleQuestions { get; set; } = true;

        public bool ShuffleOptions { get; set; } = true;

        public bool RequireFullscreen { get; set; }

        public int? MaxWarningCount { get; set; } = 3;

        public ResultReleaseMode ResultReleaseMode { get; set; } = ResultReleaseMode.AfterExamClosed;

        public ExamStatus Status { get; set; } = ExamStatus.Draft;
    }

    public class ExamQuestionDto
    {
        public int Id { get; set; }

        public int ExamId { get; set; }

        public int QuestionId { get; set; }

        public decimal Score { get; set; }

        public int DisplayOrder { get; set; }
    }

    public class ExamQuestionUpsertRequest
    {
        [Required]
        public int ExamId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public decimal Score { get; set; } = 1;

        public int DisplayOrder { get; set; } = 1;
    }

    public class ExamAttemptDto
    {
        public int Id { get; set; }

        public int ExamId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public ExamAttemptStatus Status { get; set; }

        public bool IsAutoSubmitted { get; set; }
    }

    public class ExamAttemptUpsertRequest
    {
        [Required]
        public int ExamId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }

        public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;

        public bool IsAutoSubmitted { get; set; }
    }

    public class AttemptAnswerDto
    {
        public int Id { get; set; }

        public int ExamAttemptId { get; set; }

        public int QuestionId { get; set; }

        public bool? IsCorrect { get; set; }

        public decimal? AwardedScore { get; set; }

        public DateTime LastSavedAt { get; set; }
    }

    public class AttemptAnswerUpsertRequest
    {
        [Required]
        public int ExamAttemptId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public bool? IsCorrect { get; set; }

        public decimal? AwardedScore { get; set; }
    }

    public class AntiCheatEventDto
    {
        public int Id { get; set; }

        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; }

        public AntiCheatSeverity Severity { get; set; }

        public string? Description { get; set; }

        public DateTime OccurredAt { get; set; }
    }

    public class AntiCheatEventUpsertRequest
    {
        [Required]
        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; } = AntiCheatEventType.TabHidden;

        public AntiCheatSeverity Severity { get; set; } = AntiCheatSeverity.Low;

        [StringLength(1000)]
        public string? Description { get; set; }

        public string? MetadataJson { get; set; }
    }

    public class FeatureStatusResponse
    {
        public IReadOnlyList<FeatureStatusItem> Items { get; set; } = Array.Empty<FeatureStatusItem>();
    }

    public class FeatureStatusItem
    {
        public string Name { get; set; } = string.Empty;

        public bool ApiTestable { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Endpoint { get; set; }

        public string? Note { get; set; }
    }

    public class SearchSuggestionApiItem
    {
        public string Type { get; set; } = string.Empty;

        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Meta { get; set; } = string.Empty;
    }

    public class FeatureAntiCheatReportRequest
    {
        [Required]
        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; } = AntiCheatEventType.TabHidden;

        [StringLength(1000)]
        public string? Note { get; set; }
    }

    public class SignalRStatusResponse
    {
        public string HubUrl { get; set; } = "/hubs/quiz-chat";

        public bool BrowserClientExists { get; set; }

        public string TestNote { get; set; } = string.Empty;
    }
}
