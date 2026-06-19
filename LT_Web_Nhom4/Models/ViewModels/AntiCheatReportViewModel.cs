using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class AntiCheatReportViewModel
    {
        [Range(1, int.MaxValue)]
        public int ExamId { get; set; }

        [Range(1, int.MaxValue)]
        public int ExamAttemptId { get; set; }

        public AntiCheatEventType EventType { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
