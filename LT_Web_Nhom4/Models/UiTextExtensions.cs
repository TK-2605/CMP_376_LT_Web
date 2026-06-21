namespace LT_Web_Nhom4.Models
{
    public static class UiTextExtensions
    {
        public static string ToVietnamese(this ExamStatus value) => value switch
        {
            ExamStatus.Draft => "Bản nháp",
            ExamStatus.Published => "Đã công bố",
            ExamStatus.Closed => "Đã đóng",
            ExamStatus.Cancelled => "Đã hủy",
            _ => "Không xác định"
        };

        public static string ToVietnamese(this ExamAttemptStatus value) => value switch
        {
            ExamAttemptStatus.InProgress => "Đang làm",
            ExamAttemptStatus.Submitted => "Đã nộp",
            ExamAttemptStatus.AutoSubmitted => "Tự động nộp",
            ExamAttemptStatus.Cancelled => "Đã hủy",
            _ => "Không xác định"
        };

        public static string ToVietnamese(this ResultReleaseMode value) => value switch
        {
            ResultReleaseMode.Immediately => "Ngay sau khi nộp",
            ResultReleaseMode.AfterExamClosed => "Sau khi đề đóng",
            ResultReleaseMode.Manual => "Khi chủ lớp công bố",
            _ => "Không xác định"
        };

        public static string ToVietnamese(this QuestionType value) => value switch
        {
            QuestionType.SingleChoice => "Một đáp án",
            QuestionType.MultipleChoice => "Nhiều đáp án",
            _ => "Không xác định"
        };

        public static string ToVietnamese(this QuestionDifficulty value) => value switch
        {
            QuestionDifficulty.Easy => "Dễ",
            QuestionDifficulty.Medium => "Trung bình",
            QuestionDifficulty.Hard => "Khó",
            _ => "Không xác định"
        };

        public static string ToVietnamese(this AntiCheatEventType value) => value switch
        {
            AntiCheatEventType.TabHidden => "Rời khỏi tab",
            AntiCheatEventType.WindowBlur => "Mất tập trung cửa sổ",
            AntiCheatEventType.FullscreenExited => "Thoát toàn màn hình",
            AntiCheatEventType.CopyAttempt => "Sao chép nội dung",
            AntiCheatEventType.PasteAttempt => "Dán nội dung",
            AntiCheatEventType.ConnectionLost => "Mất kết nối",
            AntiCheatEventType.MultipleDevice => "Nhiều thiết bị",
            AntiCheatEventType.IpChanged => "Thay đổi địa chỉ IP",
            _ => "Sự kiện khác"
        };
    }
}
