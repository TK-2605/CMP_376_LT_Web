namespace LT_Web_Nhom4.Models
{
    public class Class
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }

        public string TeacherId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? CoverImagePath { get; set; }

        public string? IntroVideoUrl { get; set; }

        public string? Semester { get; set; }

        public string? AcademicYear { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Subject Subject { get; set; } = null!;

        public ApplicationUser Teacher { get; set; } = null!;

        public ICollection<ClassMember> Members { get; set; } = new List<ClassMember>();

        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
