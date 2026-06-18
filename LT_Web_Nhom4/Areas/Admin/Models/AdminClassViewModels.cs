using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminClassViewModel
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public string SubjectName { get; set; } = string.Empty;

        public string TeacherId { get; set; } = string.Empty;

        public string TeacherName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Semester { get; set; }

        [StringLength(50)]
        public string? AcademicYear { get; set; }

        public int MemberCount { get; set; }

        public int ExamCount { get; set; }
    }
}
