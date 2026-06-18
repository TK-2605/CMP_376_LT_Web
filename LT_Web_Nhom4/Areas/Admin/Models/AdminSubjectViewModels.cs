using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminSubjectViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int ClassCount { get; set; }

        public int QuestionCount { get; set; }

        public int ExamCount { get; set; }
    }
}
