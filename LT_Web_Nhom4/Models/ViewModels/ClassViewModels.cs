using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ClassListViewModel
    {
        public IList<ClassCardViewModel> OwnedClasses { get; set; } = new List<ClassCardViewModel>();

        public IList<ClassCardViewModel> ParticipatingClasses { get; set; } = new List<ClassCardViewModel>();

        public JoinClassViewModel JoinClass { get; set; } = new();
    }

    public class ClassCardViewModel
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string? Semester { get; set; }

        public string? AcademicYear { get; set; }

        public int ExamCount { get; set; }

        public int MemberCount { get; set; }
    }

    public class ClassDetailsViewModel : ClassCardViewModel
    {
        public bool IsOwner { get; set; }

        public IList<ExamCardViewModel> Exams { get; set; } = new List<ExamCardViewModel>();

        public IList<ClassMemberItemViewModel> Members { get; set; } = new List<ClassMemberItemViewModel>();
    }

    public class ClassMemberItemViewModel
    {
        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    public class CreateClassViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã lớp.")]
        [Display(Name = "Mã lớp")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên lớp.")]
        [Display(Name = "Tên lớp")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Học kỳ")]
        public string? Semester { get; set; }

        [Display(Name = "Năm học")]
        public string? AcademicYear { get; set; }
    }

    public class JoinClassViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã lớp.")]
        [Display(Name = "Mã lớp")]
        public string Code { get; set; } = string.Empty;
    }
}
