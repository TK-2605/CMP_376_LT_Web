using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

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

        public string? Description { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? Semester { get; set; }

        public string? AcademicYear { get; set; }

        public int ExamCount { get; set; }

        public int MemberCount { get; set; }
    }

    public class ClassDetailsViewModel : ClassCardViewModel
    {
        public bool IsOwner { get; set; }

        public string? IntroVideoUrl { get; set; }

        public IList<ExamCardViewModel> Exams { get; set; } = new List<ExamCardViewModel>();

        public IList<ClassMemberItemViewModel> Members { get; set; } = new List<ClassMemberItemViewModel>();
    }

    public class ClassMemberItemViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    public class SubjectOptionViewModel
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    public class CreateClassViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
        [Display(Name = "Môn học")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên lớp.")]
        [StringLength(200, ErrorMessage = "Tên lớp không được vượt quá 200 ký tự.")]
        [Display(Name = "Tên lớp")]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2.000 ký tự.")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Học kỳ")]
        public string? Semester { get; set; }

        [Display(Name = "Năm học")]
        public string? AcademicYear { get; set; }

        [Url(ErrorMessage = "Liên kết video không hợp lệ.")]
        [Display(Name = "Video giới thiệu")]
        public string? IntroVideoUrl { get; set; }

        [Display(Name = "Ảnh bìa")]
        public IFormFile? CoverImage { get; set; }

        public IList<SubjectOptionViewModel> Subjects { get; set; } = new List<SubjectOptionViewModel>();
    }

    public class EditClassViewModel : CreateClassViewModel
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? ExistingCoverImageUrl { get; set; }

        public bool RemoveCoverImage { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class JoinClassViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã lớp.")]
        [RegularExpression("^[A-Za-z0-9]{3}-[A-Za-z0-9]{3}-[A-Za-z0-9]{3}$", ErrorMessage = "Mã lớp phải có dạng XXX-XXX-XXX.")]
        [Display(Name = "Mã lớp")]
        public string Code { get; set; } = string.Empty;
    }
}
