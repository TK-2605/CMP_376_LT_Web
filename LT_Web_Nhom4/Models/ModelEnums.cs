namespace LT_Web_Nhom4.Models
{
    public enum OtpPurpose
    {
        ConfirmEmail = 1,
        ForgotPassword = 2,
        LoginOtp = 3
    }

    public enum ClassMemberStatus
    {
        Active = 1,
        Removed = 2,
        Pending = 3
    }

    public enum QuestionDifficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3
    }

    public enum QuestionType
    {
        SingleChoice = 1,
        MultipleChoice = 2
    }

    public enum MediaAssetType
    {
        Image = 1,
        Video = 2
    }

    public enum QuestionStatus
    {
        Draft = 1,
        Published = 2,
        Archived = 3
    }

    public enum ExamStatus
    {
        Draft = 1,
        Published = 2,
        Closed = 3,
        Cancelled = 4
    }

    public enum ResultReleaseMode
    {
        Immediately = 1,
        AfterExamClosed = 2,
        Manual = 3
    }

    public enum ExamAttemptStatus
    {
        InProgress = 1,
        Submitted = 2,
        AutoSubmitted = 3,
        Cancelled = 4
    }

    public enum AntiCheatEventType
    {
        TabHidden = 1,
        WindowBlur = 2,
        FullscreenExited = 3,
        CopyAttempt = 4,
        PasteAttempt = 5,
        ConnectionLost = 6,
        MultipleDevice = 7,
        IpChanged = 8
    }

    public enum AntiCheatSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3
    }
}
